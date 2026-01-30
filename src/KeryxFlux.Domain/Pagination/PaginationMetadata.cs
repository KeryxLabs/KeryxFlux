namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Pagination metadata extracted from a response.
/// Contains state information about the current page and navigation to subsequent pages.
/// </summary>
public sealed class PaginationMetadata
{
    /// <summary>
    /// Current page number (0-based or 1-based depending on API)
    /// </summary>
    public int? CurrentPage { get; set; }

    /// <summary>
    /// Total number of pages
    /// </summary>
    public int? TotalPages { get; set; }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Total number of items across all pages
    /// </summary>
    public int? TotalCount { get; set; }

    /// <summary>
    /// Current offset (for offset-based pagination)
    /// </summary>
    public int? Offset { get; set; }

    /// <summary>
    /// Cursor/token for next page (for cursor-based pagination)
    /// </summary>
    public string? NextCursor { get; set; }

    /// <summary>
    /// Direct URL to next page (for link-based pagination)
    /// </summary>
    public string? NextPageUrl { get; set; }

    /// <summary>
    /// Whether there are more pages available
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Number of items in current page
    /// </summary>
    public int? CurrentPageItemCount { get; set; }

    /// <summary>
    /// Additional custom metadata from the response
    /// </summary>
    public Dictionary<string, string> CustomData { get; set; } = new();
}
