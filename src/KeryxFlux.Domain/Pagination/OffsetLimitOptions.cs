namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Configuration options for offset/limit pagination strategy.
/// Allows customization of field names, query parameters, and pagination style.
/// </summary>
public sealed class OffsetLimitOptions
{
    /// <summary>
    /// JSON field name for current page number (default: "page")
    /// </summary>
    public string PageFieldName { get; set; } = "page";

    /// <summary>
    /// JSON field name for page size (default: "size")
    /// </summary>
    public string PageSizeFieldName { get; set; } = "size";

    /// <summary>
    /// JSON field name for total pages (default: "total_pages")
    /// </summary>
    public string TotalPagesFieldName { get; set; } = "total_pages";

    /// <summary>
    /// JSON field name for total count (default: "total")
    /// </summary>
    public string TotalCountFieldName { get; set; } = "total";

    /// <summary>
    /// JSON field name for offset (default: "offset")
    /// </summary>
    public string OffsetFieldName { get; set; } = "offset";

    /// <summary>
    /// Query parameter name for page (default: "page")
    /// </summary>
    public string PageQueryParam { get; set; } = "page";

    /// <summary>
    /// Query parameter name for page size (default: "size")
    /// </summary>
    public string PageSizeQueryParam { get; set; } = "size";

    /// <summary>
    /// Query parameter name for offset (default: "offset")
    /// </summary>
    public string OffsetQueryParam { get; set; } = "offset";

    /// <summary>
    /// Query parameter name for limit (default: "limit")
    /// </summary>
    public string LimitQueryParam { get; set; } = "limit";

    /// <summary>
    /// Whether pages are 0-based (true) or 1-based (false). Default: false (1-based)
    /// </summary>
    public bool ZeroBasedPages { get; set; } = false;

    // ===== Path-based pagination options =====

    /// <summary>
    /// Use path parameters instead of query parameters.
    /// Example: /api/data/{page}/{size} instead of /api/data?page=1&size=100
    /// </summary>
    public bool UsePathParameters { get; set; } = false;

    /// <summary>
    /// Path placeholder for page number (default: "page")
    /// Example: /Medications/PAT123/{page}/{size}
    /// </summary>
    public string PagePathPlaceholder { get; set; } = "page";

    /// <summary>
    /// Path placeholder for page size (default: "size")
    /// Example: /Medications/PAT123/{page}/{size}
    /// </summary>
    public string PageSizePathPlaceholder { get; set; } = "size";

    /// <summary>
    /// Path placeholder for offset (default: "offset")
    /// Example: /api/data/{offset}/{limit}
    /// </summary>
    public string OffsetPathPlaceholder { get; set; } = "offset";

    /// <summary>
    /// Path placeholder for limit (default: "limit")
    /// Example: /api/data/{offset}/{limit}
    /// </summary>
    public string LimitPathPlaceholder { get; set; } = "limit";
}
