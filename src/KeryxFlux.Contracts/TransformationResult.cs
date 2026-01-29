namespace KeryxFlux.Contracts;

/// <summary>
/// Result of a plugin transformation operation.
/// Contains either successfully transformed data or error information.
/// </summary>
public sealed class TransformationResult
{
    /// <summary>
    /// Indicates whether the transformation was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Transformed data (available when IsSuccess = true)
    /// </summary>
    public byte[]? Data { get; }

    /// <summary>
    /// Content type of the transformed data (e.g., "application/json", "application/hl7-v2")
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Error message (available when IsSuccess = false)
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Additional metadata to attach to the transformed message
    /// (e.g., extracted patient ID, message type, etc.)
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    private TransformationResult(
        bool isSuccess,
        byte[]? data,
        string? contentType,
        string? errorMessage,
        IReadOnlyDictionary<string, string>? metadata)
    {
        IsSuccess = isSuccess;
        Data = data;
        ContentType = contentType;
        ErrorMessage = errorMessage;
        Metadata = metadata ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Create a successful transformation result
    /// </summary>
    public static TransformationResult Success(
        byte[] data,
        string contentType = "application/octet-stream",
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new TransformationResult(true, data, contentType, null, metadata);
    }

    /// <summary>
    /// Create a failed transformation result
    /// </summary>
    public static TransformationResult Failure(
        string errorMessage,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new TransformationResult(false, null, null, errorMessage, metadata);
    }
}
