using KeryxFlux.Contracts;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Ports;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;

/// <summary>
/// RabbitMQ receiver implementation using MassTransit.
/// Consumes messages from RabbitMQ queues and processes them through the plugin pipeline.
/// </summary>
public class RabbitMqReceiver : IReceiver
{
    public string Type => "rabbitmq";

    private readonly IRabbitMqConnectionService _connectionService;
    private readonly ILogger<RabbitMqReceiver> _logger;
    private readonly ReceiverConfiguration _config;
    private readonly IReceiverPlugin _plugin;
    private readonly Docket _docket;
    private IBusControl? _bus;

    public RabbitMqReceiver(
        IRabbitMqConnectionService connectionService,
        ILogger<RabbitMqReceiver> logger,
        ReceiverConfiguration config,
        IReceiverPlugin plugin,
        Docket docket)
    {
        _connectionService = connectionService;
        _logger = logger;
        _config = config;
        _plugin = plugin;
        _docket = docket;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_config.ConnectionString))
        {
            throw new InvalidOperationException("RabbitMQ connection_string is required");
        }

        if (string.IsNullOrEmpty(_config.QueueName))
        {
            throw new InvalidOperationException("RabbitMQ queue_name is required");
        }

        _logger.LogInformation(
            "Starting RabbitMQ receiver for queue: {QueueName} on {ConnectionString}",
            _config.QueueName,
            MaskConnectionString(_config.ConnectionString));

        // Configure RabbitMQ consumer
        _bus = await _connectionService.GetOrCreateBusAsync(_config.ConnectionString, cfg =>
        {
            cfg.ReceiveEndpoint(_config.QueueName, e =>
            {
                // Configure queue properties
                e.Durable = _config.Durable ?? true;
                e.AutoDelete = _config.AutoDelete ?? false;

                // Set prefetch count if specified
                if (_config.PrefetchCount.HasValue)
                {
                    e.PrefetchCount = (ushort)_config.PrefetchCount.Value;
                }

                // Set concurrent consumers
                if (_config.ConcurrentConsumers.HasValue)
                {
                    e.ConcurrentMessageLimit = _config.ConcurrentConsumers.Value;
                }

                // Bind to exchange if configured
                if (!string.IsNullOrEmpty(_config.ExchangeName))
                {
                    e.Bind(_config.ExchangeName, x =>
                    {
                        if (!string.IsNullOrEmpty(_config.RoutingKey))
                        {
                            x.RoutingKey = _config.RoutingKey;
                        }
                        
                        if (!string.IsNullOrEmpty(_config.ExchangeType))
                        {
                            x.ExchangeType = _config.ExchangeType;
                        }
                    });
                }

                // Register message handler for byte array messages
                e.Handler<byte[]>(async context =>
                {
                    await HandleMessageAsync(context.Message, cancellationToken);
                });
            });
        });

        _logger.LogInformation("RabbitMQ receiver started successfully for queue: {QueueName}", _config.QueueName);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping RabbitMQ receiver for queue: {QueueName}", _config.QueueName);

        if (_bus != null)
        {
            await _bus.StopAsync(cancellationToken);
        }

        _logger.LogInformation("RabbitMQ receiver stopped");
    }

    private async Task HandleMessageAsync(byte[] payload, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Received message from RabbitMQ queue {QueueName}. Size: {Size} bytes",
                _config.QueueName,
                payload.Length);

            // Create transformation context
            var context = new TransformationContext
            {
                DocketName = _docket.Name,
                ReceiverType = "rabbitmq",
                ReceivedAt = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid().ToString(),
                DocketConfiguration = _docket.Configuration ?? new Dictionary<string, string>(),
                SourceEndpoint = _config.QueueName
            };

            // Transform through plugin
            var result = _plugin.Transform(payload, context);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Successfully processed RabbitMQ message from queue {QueueName}",
                    _config.QueueName);

                // TODO: Forward to destinations if configured
                // This will be handled by the forwarding layer
            }
            else
            {
                _logger.LogError(
                    "Failed to transform RabbitMQ message from queue {QueueName}. Error: {Error}",
                    _config.QueueName,
                    result.ErrorMessage);

                // Message will be requeued or sent to DLQ based on RabbitMQ configuration
                throw new InvalidOperationException($"Transformation failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing RabbitMQ message from queue {QueueName}",
                _config.QueueName);
            throw; // Let MassTransit handle retry/DLQ
        }
    }

    private static string MaskConnectionString(string connectionString)
    {
        try
        {
            var uri = new Uri(connectionString);
            return $"amqp://***:***@{uri.Host}:{uri.Port}{uri.AbsolutePath}";
        }
        catch
        {
            return "***";
        }
    }
}
