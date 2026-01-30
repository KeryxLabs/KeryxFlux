namespace KeryxFlux.Contracts;

/// <summary>
/// Context information about pagination state when processing paginated workflows.
/// Provides plugins with awareness of which page they're processing.
/// </summary>
public sealed class PaginationContext
{
    /// <summary>
    /// Current page number being processed (1-based or 0-based depending on API)
    /// </summary>
    public required int CurrentPage { get; init; }

    /// <summary>
    /// Total number of pages (if known from API response)
    /// </summary>
    public int? TotalPages { get; init; }

    /// <summary>
    /// Total number of items across all pages (if known)
    /// </summary>
    public int? TotalItems { get; init; }

    /// <summary>
    /// Number of items in the current page
    /// </summary>
    public int? PageSize { get; init; }

    /// <summary>
    /// Pagination strategy being used
    /// </summary>
    public required string Strategy { get; init; }

    /// <summary>
    /// Whether this is the first page
    /// </summary>
    public bool IsFirstPage { get; init; }

    /// <summary>
    /// Whether this is the last page
    /// </summary>
    public bool IsLastPage { get; init; }

    /// <summary>
    /// Cursor/token for current page (for cursor-based pagination)
    /// </summary>
    public string? CurrentCursor { get; init; }

    /// <summary>
    /// Cursor/token for next page (if available)
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Additional pagination metadata specific to the strategy
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalData { get; init; } = new Dictionary<string, string>();
}
