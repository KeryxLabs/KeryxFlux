namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Strategy for handling different pagination patterns.
/// Implementations determine how to extract pagination info from responses
/// and build next page requests.
/// </summary>
public interface IPaginationStrategy
{
    /// <summary>
    /// Type identifier for this pagination strategy
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Extract pagination metadata from a response
    /// </summary>
    /// <param name="responseData">HTTP response body</param>
    /// <param name="responseHeaders">HTTP response headers (optional)</param>
    /// <returns>Pagination metadata</returns>
    PaginationMetadata ExtractMetadata(byte[] responseData, IDictionary<string, string>? responseHeaders = null);

    /// <summary>
    /// Build the next page URL based on current metadata
    /// </summary>
    /// <param name="baseUrl">Base URL template</param>
    /// <param name="currentMetadata">Current page metadata</param>
    /// <returns>URL for next page, or null if no more pages</returns>
    string? BuildNextPageUrl(string baseUrl, PaginationMetadata currentMetadata);

    /// <summary>
    /// Check if there are more pages to fetch
    /// </summary>
    bool HasMorePages(PaginationMetadata metadata);
}

/// <summary>
/// Pagination metadata extracted from a response
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
