namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Represents a message to be sent to a destination
/// </summary>
public sealed class OutboundMessage
{
    /// <summary>
    /// Destination name (from docket configuration)
    /// </summary>
    public required string DestinationName { get; init; }

    /// <summary>
    /// Transformed payload to send
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    /// Content type of the payload
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>
    /// Additional headers/properties to include in the outbound message
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Correlation ID for tracing (propagated from received message)
    /// </summary>
    public required string CorrelationId { get; init; }

    /// <summary>
    /// Timeout for the send operation
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}

/// <summary>
/// Result of a send operation
/// </summary>
public sealed class SendResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Exception? Exception { get; }

    private SendResult(bool isSuccess, string? errorMessage, Exception? exception)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Exception = exception;
    }

    public static SendResult Success() => new(true, null, null);

    public static SendResult Failure(string errorMessage, Exception? exception = null) 
        => new(false, errorMessage, exception);
}
