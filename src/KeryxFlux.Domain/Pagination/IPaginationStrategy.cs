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

