using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;

/// <summary>
/// Service for managing RabbitMQ consumers for receiver dockets.
/// Dynamically registers/unregisters MassTransit consumers as dockets are loaded/unloaded.
/// Similar to JobRegistrationService for pollers, but for RabbitMQ receivers.
/// </summary>
public class RabbitMqReceiver : IRabbitMqReceiverService
{
    private readonly IRabbitMqConnectionService _connectionService;
    private readonly ILogger<RabbitMqReceiver> _logger;
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly IMediator _mediator;
    
    // Track active consumers: docketName ? (queueName, busControl)
    private readonly ConcurrentDictionary<string, (string QueueName, IBusControl Bus)> _activeConsumers = new();

    public RabbitMqReceiver(
        IRabbitMqConnectionService connectionService,
        ILogger<RabbitMqReceiver> logger,
        IDocketManager docketManager,
        IPluginManager pluginManager,
        IMediator mediator)
    {
        _connectionService = connectionService;
        _logger = logger;
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _mediator = mediator;
    }

    /// <summary>
    /// Register a RabbitMQ consumer for the specified docket.
    /// Called by DocketOrchestrationService when a receiver docket is loaded.
    /// </summary>
    public async Task RegisterConsumerForDocketAsync(Docket docket)
    {
        if (docket.Receiver == null)
        {
            throw new InvalidOperationException($"Docket {docket.Name} has no receiver configuration");
        }

        var config = docket.Receiver;

        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            throw new InvalidOperationException($"RabbitMQ connection_string is required for docket {docket.Name}");
        }

        if (string.IsNullOrEmpty(config.QueueName))
        {
            throw new InvalidOperationException($"RabbitMQ queue_name is required for docket {docket.Name}");
        }

        _logger.LogInformation(
            "Registering RabbitMQ consumer for docket: {DocketName}, queue: {QueueName}",
            docket.Name,
            config.QueueName);

        // Create MassTransit bus with consumer
        var bus = await _connectionService.GetOrCreateBusAsync(config.ConnectionString, cfg =>
        {
            cfg.ReceiveEndpoint(config.QueueName, e =>
            {
                // Configure queue properties
                e.Durable = config.Durable ?? true;
                e.AutoDelete = config.AutoDelete ?? false;

                if (config.PrefetchCount.HasValue)
                {
                    e.PrefetchCount = (ushort)config.PrefetchCount.Value;
                }

                if (config.ConcurrentConsumers.HasValue)
                {
                    e.ConcurrentMessageLimit = config.ConcurrentConsumers.Value;
                }

                // Bind to exchange if configured
                if (!string.IsNullOrEmpty(config.ExchangeName))
                {
                    e.Bind(config.ExchangeName, x =>
                    {
                        if (!string.IsNullOrEmpty(config.RoutingKey))
                        {
                            x.RoutingKey = config.RoutingKey;
                        }
                        
                        if (!string.IsNullOrEmpty(config.ExchangeType))
                        {
                            x.ExchangeType = config.ExchangeType;
                        }
                    });
                }

                // Register generic message handler
                e.Handler<byte[]>(async context =>
                {
                    await HandleMessageAsync(docket.Name, context.Message);
                });
            });
        });

        // Track the consumer
        _activeConsumers[docket.Name] = (config.QueueName, bus);

        _logger.LogInformation(
            "RabbitMQ consumer registered successfully for docket: {DocketName}, queue: {QueueName}",
            docket.Name,
            config.QueueName);
    }

    /// <summary>
    /// Unregister RabbitMQ consumer for the specified docket.
    /// Called by DocketOrchestrationService when a docket is unloaded.
    /// </summary>
    public async Task UnregisterConsumerForDocketAsync(string docketName)
    {
        if (!_activeConsumers.TryRemove(docketName, out var consumer))
        {
            _logger.LogWarning("No active consumer found for docket: {DocketName}", docketName);
            return;
        }

        _logger.LogInformation("Unregistering RabbitMQ consumer for docket: {DocketName}", docketName);

        try
        {
            await consumer.Bus.StopAsync();
            _logger.LogInformation("RabbitMQ consumer stopped for docket: {DocketName}", docketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping RabbitMQ consumer for docket: {DocketName}", docketName);
        }
    }

    private async Task HandleMessageAsync(string docketName, byte[] payload)
    {
        try
        {
            _logger.LogInformation(
                "Received RabbitMQ message for docket: {DocketName}, size: {Size} bytes",
                docketName,
                payload.Length);

            // Get the docket
            if (!_docketManager.TryGetDocket(docketName, out var docket))
            {
                _logger.LogError("Docket not found: {DocketName}", docketName);
                throw new InvalidOperationException($"Docket '{docketName}' not found");
            }

            // Load plugin
            var pluginResult = _pluginManager.LoadPlugin(docket.PluginLocation);
            if (!pluginResult.IsSuccess || pluginResult.Value is not IReceiverPlugin plugin)
            {
                _logger.LogError("Failed to load receiver plugin for docket {DocketName}: {Error}", 
                    docketName, pluginResult.Error);
                throw new InvalidOperationException($"Plugin load failed: {pluginResult.Error}");
            }

            // Create ReceivedMessage
            var message = new ReceivedMessage
            {
                Payload = payload,
                ContentType = "application/octet-stream", // RabbitMQ doesn't enforce content type
                CorrelationId = Guid.NewGuid().ToString(),
                SourceEndpoint = docket.Receiver?.QueueName ?? "unknown",
                Metadata = new Dictionary<string, string>
                {
                    { "source", "rabbitmq" },
                    { "queue", docket.Receiver?.QueueName ?? "unknown" }
                }
            };

            // Send to MediatR pipeline (reuses ProcessMessageCommandHandler)
            var command = new ProcessMessageCommand
            {
                DocketName = docketName,
                Message = message
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Successfully processed RabbitMQ message for docket {DocketName}. Forwarded to {Count} destinations.",
                    docketName,
                    result.DestinationsForwarded);
            }
            else
            {
                _logger.LogError(
                    "Failed to process RabbitMQ message for docket {DocketName}: {Error}",
                    docketName,
                    result.ErrorMessage);
                throw new InvalidOperationException($"Message processing failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RabbitMQ message for docket: {DocketName}", docketName);
            throw; // Let MassTransit handle retry/DLQ
        }
    }
}
