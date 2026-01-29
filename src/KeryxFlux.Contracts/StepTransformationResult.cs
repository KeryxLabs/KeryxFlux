namespace KeryxFlux.Contracts;

/// <summary>
/// Result of transforming a single step in a multi-step workflow.
/// Plugin decides: continue with next step, or complete with final data.
/// </summary>
public sealed class StepTransformationResult
{
    /// <summary>
    /// Indicates whether the transformation was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether this item's workflow is complete
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>
    /// The next step to execute (if IsComplete = false)
    /// </summary>
    public NextStep? ContinuationStep { get; }

    /// <summary>
    /// Final transformed data (if IsComplete = true)
    /// </summary>
    public byte[]? FinalData { get; }

    /// <summary>
    /// Content type of final data
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Error message (if IsSuccess = false)
    /// </summary>
    public string? ErrorMessage { get; }

    private StepTransformationResult(
        bool isSuccess,
        bool isComplete,
        NextStep? continuationStep,
        byte[]? finalData,
        string? contentType,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        IsComplete = isComplete;
        ContinuationStep = continuationStep;
        FinalData = finalData;
        ContentType = contentType;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Continue workflow with the next step.
    /// System will execute this step and call TransformItemStep again.
    /// </summary>
    public static StepTransformationResult ContinueWith(NextStep nextStep)
    {
        return new StepTransformationResult(
            isSuccess: true,
            isComplete: false,
            continuationStep: nextStep,
            finalData: null,
            contentType: null,
            errorMessage: null);
    }

    /// <summary>
    /// Complete this item's workflow with final data.
    /// System will forward this data to configured destinations.
    /// </summary>
    public static StepTransformationResult Complete(byte[] finalData, string contentType)
    {
        return new StepTransformationResult(
            isSuccess: true,
            isComplete: true,
            continuationStep: null,
            finalData: finalData,
            contentType: contentType,
            errorMessage: null);
    }

    /// <summary>
    /// Indicate step processing failed.
    /// This item's workflow will be aborted (other items continue).
    /// </summary>
    public static StepTransformationResult Failure(string errorMessage)
    {
        return new StepTransformationResult(
            isSuccess: false,
            isComplete: false,
            continuationStep: null,
            finalData: null,
            contentType: null,
            errorMessage: errorMessage);
    }
}

/// <summary>
/// Represents the next step to execute in a multi-step workflow
/// </summary>
public sealed class NextStep
{
    /// <summary>
    /// Name of this step (for logging/debugging)
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL path to request (relative or absolute)
    /// </summary>
    public required string RequestUrl { get; init; }

    /// <summary>
    /// HTTP method (GET, POST, etc.)
    /// </summary>
    public string Method { get; init; } = "GET";

    /// <summary>
    /// Optional request body for this step
    /// </summary>
    public byte[]? RequestBody { get; init; }

    /// <summary>
    /// Optional headers for this step
    /// </summary>
    public Dictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// Metadata to help plugin identify this step in next invocation
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
