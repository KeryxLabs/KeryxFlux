namespace KeryxFlux.Contracts;

/// <summary>
/// Result of parsing the initial polling response.
/// Contains individual items that will each become independent workflows.
/// </summary>
public sealed class InitialPollingResult
{
    /// <summary>
    /// Indicates whether parsing was successful
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Extracted items (e.g., list of jobs, orders, articles) 
    /// Each item becomes an independent workflow with its own steps and state.
    /// </summary>
    public IReadOnlyList<PollingItem> Items { get; }

    /// <summary>
    /// Error message (if IsSuccess = false)
    /// </summary>
    public string? ErrorMessage { get; }

    private InitialPollingResult(
        bool isSuccess,
        IReadOnlyList<PollingItem> items,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        Items = items;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Create a successful result with extracted items
    /// </summary>
    public static InitialPollingResult Success(IReadOnlyList<PollingItem> items)
    {
        return new InitialPollingResult(true, items, null);
    }

    /// <summary>
    /// Create a successful result with no items (empty poll)
    /// </summary>
    public static InitialPollingResult Empty()
    {
        return new InitialPollingResult(true, Array.Empty<PollingItem>(), null);
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static InitialPollingResult Failure(string errorMessage)
    {
        return new InitialPollingResult(false, Array.Empty<PollingItem>(), errorMessage);
    }
}

/// <summary>
/// Represents a single item extracted from initial polling response.
/// Each item becomes an independent workflow.
/// </summary>
public sealed class PollingItem
{
    /// <summary>
    /// Unique identifier for this item (e.g., patient ID, order number)
    /// Used for correlation and logging.
    /// </summary>
    public required string ItemId { get; init; }

    /// <summary>
    /// The actual item data (will be stored in item's AccumulatedState)
    /// </summary>
    public required object ItemData { get; init; }

    /// <summary>
    /// The first step to execute for this item.
    /// Example: For patient p1, start with GET /Patient/p1
    /// 
    /// Plugin will then decide continuation steps via TransformItemStep:
    /// - ContinueWithSteps([...]) for more steps
    /// - Complete(...) when done
    /// </summary>
    public required NextStep InitialStep { get; init; }

    /// <summary>
    /// Optional metadata for this item (e.g., item type, priority)
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = new();
}
