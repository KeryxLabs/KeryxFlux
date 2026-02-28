using Confluent.Kafka;
using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KeryxFlux.Infrastructure.MessageBrokers.Kafka;

/// <summary>
/// Kafka producer that forwards messages to Kafka topics using Confluent.Kafka.
/// </summary>
public class KafkaProducer : ISender
{
    public string Type => "kafka";

    private readonly ILogger<KafkaProducer> _logger;
    
    // Connection pooling: bootstrap_servers ? producer
    private readonly ConcurrentDictionary<string, IProducer<Null, byte[]>> _producers = new();

    public KafkaProducer(ILogger<KafkaProducer> logger)
    {
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var bootstrapServers = GetHeaderValue(message, "bootstrap_servers");
            var topic = GetHeaderValue(message, "topic");
            var partitionKey = GetHeaderValue(message, "partition_key");

            if (string.IsNullOrEmpty(bootstrapServers))
            {
                return SendResult.Failure("Kafka bootstrap_servers is required in message headers");
            }

            if (string.IsNullOrEmpty(topic))
            {
                return SendResult.Failure("Kafka topic is required in message headers");
            }

            _logger.LogInformation(
                "Sending message to Kafka. Topic: {Topic}, Size: {Size} bytes",
                topic,
                message.Payload.Length);

            // Get or create producer for this bootstrap server
            var producer = _producers.GetOrAdd(bootstrapServers, servers =>
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = servers,
                    Acks = Acks.All, // Wait for all in-sync replicas
                    CompressionType = CompressionType.Gzip,
                    EnableIdempotence = true, // Exactly-once semantics
                    MaxInFlight = 5,
                    MessageSendMaxRetries = 3,
                    RetryBackoffMs = 100
                };

                return new ProducerBuilder<Null, byte[]>(config)
                    .SetValueSerializer(Serializers.ByteArray)
                    .Build();
            });

            // Produce message
            var kafkaMessage = new Message<Null, byte[]>
            {
                Value = message.Payload,
                Headers = new Headers
                {
                    { "correlation_id", System.Text.Encoding.UTF8.GetBytes(message.CorrelationId) },
                    { "content_type", System.Text.Encoding.UTF8.GetBytes(message.ContentType) }
                }
            };

            var deliveryResult = await producer.ProduceAsync(topic, kafkaMessage, cancellationToken);

            _logger.LogInformation(
                "Successfully sent message to Kafka. Topic: {Topic}, Partition: {Partition}, Offset: {Offset}",
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value);

            return SendResult.Success();
        }
        catch (ProduceException<Null, byte[]> ex)
        {
            _logger.LogError(ex,
                "Kafka produce error for destination: {Destination}. Error: {Error}",
                message.DestinationName,
                ex.Error.Reason);

            return SendResult.Failure($"Kafka produce failed: {ex.Error.Reason}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send message to Kafka destination: {Destination}",
                message.DestinationName);

            return SendResult.Failure($"Kafka send failed: {ex.Message}");
        }
    }

    private static string? GetHeaderValue(OutboundMessage message, string key)
    {
        return message.Headers.TryGetValue(key, out var value) ? value : null;
    }
}
