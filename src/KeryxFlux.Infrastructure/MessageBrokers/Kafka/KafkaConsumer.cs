using Confluent.Kafka;
using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KeryxFlux.Infrastructure.MessageBrokers.Kafka;

/// <summary>
/// Service for managing Kafka consumers for receiver dockets.
/// Dynamically registers/unregisters Confluent.Kafka consumers as dockets are loaded/unloaded.
/// Similar to RabbitMqReceiver but for Kafka.
/// </summary>
public class KafkaConsumer : IKafkaConsumerService
{
    private readonly ILogger<KafkaConsumer> _logger;
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly IMediator _mediator;
    
    // Track active consumers: docketName ? (topic, consumer, cancellationTokenSource)
    private readonly ConcurrentDictionary<string, (string Topic, IConsumer<Ignore, byte[]> Consumer, CancellationTokenSource Cts)> _activeConsumers = new();

    public KafkaConsumer(
        ILogger<KafkaConsumer> logger,
        IDocketManager docketManager,
        IPluginManager pluginManager,
        IMediator mediator)
    {
        _logger = logger;
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _mediator = mediator;
    }

    public async Task RegisterConsumerForDocketAsync(Docket docket)
    {
        if (docket.Receiver == null)
        {
            throw new InvalidOperationException($"Docket {docket.Name} has no receiver configuration");
        }

        var config = docket.Receiver;

        if (string.IsNullOrEmpty(config.BootstrapServers))
        {
            throw new InvalidOperationException($"Kafka bootstrap_servers is required for docket {docket.Name}");
        }

        if (string.IsNullOrEmpty(config.Topic))
        {
            throw new InvalidOperationException($"Kafka topic is required for docket {docket.Name}");
        }

        if (string.IsNullOrEmpty(config.GroupId))
        {
            throw new InvalidOperationException($"Kafka group_id is required for docket {docket.Name}");
        }

        _logger.LogInformation(
            "Registering Kafka consumer for docket: {DocketName}, topic: {Topic}, group: {GroupId}",
            docket.Name,
            config.Topic,
            config.GroupId);

        // Create Confluent.Kafka consumer config
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = config.BootstrapServers,
            GroupId = config.GroupId,
            AutoOffsetReset = config.AutoOffsetReset?.ToLowerInvariant() == "earliest" 
                ? AutoOffsetReset.Earliest 
                : AutoOffsetReset.Latest,
            EnableAutoCommit = config.EnableAutoCommit ?? true,
            EnableAutoOffsetStore = false // Manual offset management for better control
        };

        var consumer = new ConsumerBuilder<Ignore, byte[]>(consumerConfig)
            .SetValueDeserializer(Deserializers.ByteArray)
            .Build();

        consumer.Subscribe(config.Topic);

        var cts = new CancellationTokenSource();

        // Track the consumer
        _activeConsumers[docket.Name] = (config.Topic, consumer, cts);

        // Start consuming in background
        _ = Task.Run(async () => await ConsumeAsync(docket.Name, consumer, cts.Token), cts.Token);

        _logger.LogInformation(
            "Kafka consumer registered successfully for docket: {DocketName}, topic: {Topic}",
            docket.Name,
            config.Topic);

        await Task.CompletedTask;
    }

    public async Task UnregisterConsumerForDocketAsync(string docketName)
    {
        if (!_activeConsumers.TryRemove(docketName, out var consumerInfo))
        {
            _logger.LogWarning("No active Kafka consumer found for docket: {DocketName}", docketName);
            return;
        }

        _logger.LogInformation("Unregistering Kafka consumer for docket: {DocketName}", docketName);

        try
        {
            consumerInfo.Cts.Cancel();
            consumerInfo.Consumer.Close();
            consumerInfo.Consumer.Dispose();
            _logger.LogInformation("Kafka consumer stopped for docket: {DocketName}", docketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping Kafka consumer for docket: {DocketName}", docketName);
        }

        await Task.CompletedTask;
    }

    private async Task ConsumeAsync(string docketName, IConsumer<Ignore, byte[]> consumer, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(cancellationToken);
                    
                    if (consumeResult?.Message?.Value != null)
                    {
                        await HandleMessageAsync(docketName, consumeResult.Message.Value, consumeResult);
                        
                        // Manually store offset after successful processing
                        consumer.StoreOffset(consumeResult);
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error for docket {DocketName}", docketName);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation token is triggered
                    break;
                }
            }
        }
        finally
        {
            _logger.LogInformation("Kafka consumer loop ended for docket {DocketName}", docketName);
        }
    }

    private async Task HandleMessageAsync(string docketName, byte[] payload, ConsumeResult<Ignore, byte[]> consumeResult)
    {
        try
        {
            _logger.LogInformation(
                "Received Kafka message for docket: {DocketName}, topic: {Topic}, partition: {Partition}, offset: {Offset}, size: {Size} bytes",
                docketName,
                consumeResult.Topic,
                consumeResult.Partition.Value,
                consumeResult.Offset.Value,
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
                ContentType = "application/octet-stream",
                CorrelationId = Guid.NewGuid().ToString(),
                SourceEndpoint = consumeResult.Topic,
                Metadata = new Dictionary<string, string>
                {
                    { "source", "kafka" },
                    { "topic", consumeResult.Topic },
                    { "partition", consumeResult.Partition.Value.ToString() },
                    { "offset", consumeResult.Offset.Value.ToString() },
                    { "timestamp", consumeResult.Message.Timestamp.UtcDateTime.ToString("O") }
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
                    "Successfully processed Kafka message for docket {DocketName}. Forwarded to {Count} destinations.",
                    docketName,
                    result.DestinationsForwarded);
            }
            else
            {
                _logger.LogError(
                    "Failed to process Kafka message for docket {DocketName}: {Error}",
                    docketName,
                    result.ErrorMessage);
                throw new InvalidOperationException($"Message processing failed: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Kafka message for docket: {DocketName}", docketName);
            throw;
        }
    }
}
