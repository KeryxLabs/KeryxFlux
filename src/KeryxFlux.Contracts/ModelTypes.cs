namespace KeryxFlux.Contracts;

/// <summary>
/// Plan for model invocations returned by ParseInitialMessage.
/// Either starts with a model step or completes immediately without model calls.
/// </summary>
public sealed class ModelInvocationPlan
{
    public ModelStep? FirstStep { get; init; }
    public byte[]? DirectOutput { get; init; }
    public string? DirectContentType { get; init; }

    /// <summary>
    /// Create a plan that requires model invocation.
    /// </summary>
    public static ModelInvocationPlan WithStep(ModelStep step)
    {
        return new ModelInvocationPlan { FirstStep = step };
    }

    /// <summary>
    /// Create a plan that completes without model invocation.
    /// </summary>
    public static ModelInvocationPlan NoInvocation(byte[] output, string contentType = "application/json")
    {
        return new ModelInvocationPlan 
        { 
            DirectOutput = output,
            DirectContentType = contentType
        };
    }
}

/// <summary>
/// Represents a single model invocation step.
/// Plugin builds these to request HTTP/gRPC calls to model endpoints.
/// </summary>
public sealed class ModelStep
{
    public required string Name { get; init; }
    public required string Endpoint { get; init; }
    public string Method { get; init; } = "POST";
    public required byte[] RequestBody { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// Result of processing a model response.
/// Plugin decides: continue with another model call, or complete and forward.
/// </summary>
public sealed class ModelStepResult
{
    public bool IsComplete { get; init; }
    public byte[]? FinalData { get; init; }
    public string? ContentType { get; init; }
    public ModelStep? ContinuationStep { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Indicate processing is complete and ready to forward.
    /// </summary>
    public static ModelStepResult Complete(byte[] data, string contentType = "application/json")
    {
        return new ModelStepResult
        {
            IsComplete = true,
            FinalData = data,
            ContentType = contentType
        };
    }

    /// <summary>
    /// Indicate another model step is needed.
    /// </summary>
    public static ModelStepResult ContinueWith(ModelStep nextStep)
    {
        return new ModelStepResult
        {
            IsComplete = false,
            ContinuationStep = nextStep
        };
    }

    /// <summary>
    /// Indicate step processing failed.
    /// </summary>
    public static ModelStepResult Failure(string errorMessage)
    {
        return new ModelStepResult
        {
            IsComplete = false,
            ErrorMessage = errorMessage
        };
    }
}
