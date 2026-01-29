namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Represents a message received from any receiver type
/// </summary>
public sealed class ReceivedMessage
{
    /// <summary>
    /// Raw message payload (bytes)
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    /// Content type if known (e.g., "application/json", "application/hl7-v2")
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Metadata from the source (HTTP headers, message properties, etc.)
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Correlation ID for tracing (can be extracted from metadata or generated)
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when the message was received
    /// </summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Source endpoint (URL path, queue name, topic name, etc.)
    /// </summary>
    public string? SourceEndpoint { get; init; }
}
