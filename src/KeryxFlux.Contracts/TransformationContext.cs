namespace KeryxFlux.Contracts;

/// <summary>
/// Context information passed to plugins during transformation.
/// Provides metadata about the incoming request and execution environment.
/// </summary>
public sealed class TransformationContext
{
    /// <summary>
    /// Name of the docket that triggered this transformation
    /// </summary>
    public required string DocketName { get; init; }

    /// <summary>
    /// Type of receiver that captured this data (http, rabbitmq, kafka, tcp)
    /// </summary>
    public required string ReceiverType { get; init; }

    /// <summary>
    /// Timestamp when the data was received (UTC)
    /// </summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// Unique identifier for this processing instance (for tracing)
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Optional headers/metadata from the source (e.g., HTTP headers, message properties)
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Original endpoint/queue/topic where data was received
    /// </summary>
    public string? SourceEndpoint { get; init; }

    /// <summary>
    /// Create a new transformation context
    /// </summary>
    public TransformationContext()
    {
    }

    /// <summary>
    /// Create context with required fields (for easier construction)
    /// </summary>
    public static TransformationContext Create(
        string docketName,
        string receiverType,
        string correlationId,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new TransformationContext
        {
            DocketName = docketName,
            ReceiverType = receiverType,
            ReceivedAt = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }
}
