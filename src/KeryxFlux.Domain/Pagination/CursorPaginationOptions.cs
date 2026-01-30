namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Configuration options for cursor-based pagination strategy.
/// Supports nested JSON structures and custom field naming.
/// </summary>
public sealed class CursorPaginationOptions
{
    /// <summary>
    /// Path to paging object in JSON response (e.g., "paging" or "meta.pagination")
    /// Leave empty if pagination fields are at root level
    /// </summary>
    public string PagingObjectPath { get; set; } = string.Empty;

    /// <summary>
    /// JSON field name for next cursor/token (default: "next")
    /// </summary>
    public string NextCursorFieldName { get; set; } = "next";

    /// <summary>
    /// JSON field name for "has more" flag (default: "has_more")
    /// </summary>
    public string HasMoreFieldName { get; set; } = "has_more";

    /// <summary>
    /// JSON field name for page size (default: "size")
    /// </summary>
    public string PageSizeFieldName { get; set; } = "size";

    /// <summary>
    /// Query parameter name for cursor (default: "cursor")
    /// </summary>
    public string CursorQueryParam { get; set; } = "cursor";
}
