namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Port interface for receiving incoming data.
/// Implementations handle protocol-specific details (HTTP, RabbitMQ, Kafka, TCP).
/// Note: For active receivers like RabbitMQ, use dedicated service classes instead.
/// </summary>
public interface IReceiver
{
    /// <summary>
    /// Type identifier for this receiver (e.g., "http", "rabbitmq", "kafka", "tcp")
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Start receiving data.
    /// This should be non-blocking and handle data asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to signal shutdown</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop receiving data and clean up resources
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}


