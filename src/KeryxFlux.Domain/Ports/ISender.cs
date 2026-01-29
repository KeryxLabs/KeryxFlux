namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Port interface for sending/forwarding transformed data.
/// Implementations handle protocol-specific details (HTTP, RabbitMQ, Kafka, TCP).
/// </summary>
public interface ISender
{
    /// <summary>
    /// Type identifier for this sender (e.g., "http", "rabbitmq", "kafka", "tcp")
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Send transformed data to the destination
    /// </summary>
    /// <param name="message">Message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<SendResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default);
}
