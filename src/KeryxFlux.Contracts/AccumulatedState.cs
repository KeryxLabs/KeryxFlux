namespace KeryxFlux.Contracts;

/// <summary>
/// State accumulated across multiple polling steps.
/// Plugins can store intermediate data here that will be passed to subsequent steps.
/// </summary>
public sealed class AccumulatedState
{
    /// <summary>
    /// Data collected from previous steps (plugin-specific structure)
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = new();

    /// <summary>
    /// Step execution history (for debugging/audit)
    /// </summary>
    public List<StepExecution> ExecutionHistory { get; init; } = new();

    /// <summary>
    /// Current step number (0-based)
    /// </summary>
    public int CurrentStepIndex => ExecutionHistory.Count;

    /// <summary>
    /// Add data to accumulated state
    /// </summary>
    public void Add(string key, object value)
    {
        Data[key] = value;
    }

    /// <summary>
    /// Get data from accumulated state
    /// </summary>
    public T? Get<T>(string key)
    {
        if (Data.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }

    /// <summary>
    /// Record a step execution
    /// </summary>
    public void RecordStepExecution(string stepName, string? requestUrl, bool wasSuccessful)
    {
        ExecutionHistory.Add(new StepExecution
        {
            StepName = stepName,
            RequestUrl = requestUrl,
            ExecutedAt = DateTimeOffset.UtcNow,
            WasSuccessful = wasSuccessful
        });
    }
}

/// <summary>
/// Record of a single step execution
/// </summary>
public sealed class StepExecution
{
    public required string StepName { get; init; }
    public string? RequestUrl { get; init; }
    public DateTimeOffset ExecutedAt { get; init; }
    public bool WasSuccessful { get; init; }
}
