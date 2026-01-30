using System.Text.Json;

namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Offset/Limit pagination strategy.
/// Supports both page-based (page/size) and offset-based (offset/limit) pagination.
/// </summary>
/// <remarks>
/// Example API responses:
/// - { "page": 1, "size": 100, "total": 500 }
/// - { "offset": 100, "limit": 100, "total": 500 }
/// </remarks>
public sealed class OffsetLimitPaginationStrategy : IPaginationStrategy
{
    public string Type => "offset-limit";

    private readonly OffsetLimitOptions _options;

    public OffsetLimitPaginationStrategy(OffsetLimitOptions? options = null)
    {
        _options = options ?? new OffsetLimitOptions();
    }

    public PaginationMetadata ExtractMetadata(byte[] responseData, IDictionary<string, string>? responseHeaders = null)
    {
        var metadata = new PaginationMetadata();

        try
        {
            using var doc = JsonDocument.Parse(responseData);
            var root = doc.RootElement;

            // Try to extract pagination info from JSON response
            if (root.TryGetProperty(_options.PageFieldName, out var pageElement))
            {
                metadata.CurrentPage = pageElement.GetInt32();
            }

            if (root.TryGetProperty(_options.PageSizeFieldName, out var sizeElement))
            {
                metadata.PageSize = sizeElement.GetInt32();
            }

            if (root.TryGetProperty(_options.TotalPagesFieldName, out var totalPagesElement))
            {
                metadata.TotalPages = totalPagesElement.GetInt32();
            }

            if (root.TryGetProperty(_options.TotalCountFieldName, out var totalCountElement))
            {
                metadata.TotalCount = totalCountElement.GetInt32();
            }

            if (root.TryGetProperty(_options.OffsetFieldName, out var offsetElement))
            {
                metadata.Offset = offsetElement.GetInt32();
            }

            // Determine if there are more pages
            if (metadata.CurrentPage.HasValue && metadata.TotalPages.HasValue)
            {
                metadata.HasMore = metadata.CurrentPage.Value < metadata.TotalPages.Value - (_options.ZeroBasedPages ? 0 : 1);
            }
            else if (metadata.Offset.HasValue && metadata.PageSize.HasValue && metadata.TotalCount.HasValue)
            {
                metadata.HasMore = metadata.Offset.Value + metadata.PageSize.Value < metadata.TotalCount.Value;
            }
        }
        catch (JsonException)
        {
            // If JSON parsing fails, assume no more pages
            metadata.HasMore = false;
        }

        return metadata;
    }

    public string? BuildNextPageUrl(string baseUrl, PaginationMetadata currentMetadata)
    {
        if (!HasMorePages(currentMetadata))
        {
            return null;
        }

        // Build URL based on page or offset
        if (currentMetadata.CurrentPage.HasValue && currentMetadata.PageSize.HasValue)
        {
            var nextPage = currentMetadata.CurrentPage.Value + 1;
            var separator = baseUrl.Contains('?') ? "&" : "?";
            return $"{baseUrl}{separator}{_options.PageQueryParam}={nextPage}&{_options.PageSizeQueryParam}={currentMetadata.PageSize.Value}";
        }
        else if (currentMetadata.Offset.HasValue && currentMetadata.PageSize.HasValue)
        {
            var nextOffset = currentMetadata.Offset.Value + currentMetadata.PageSize.Value;
            var separator = baseUrl.Contains('?') ? "&" : "?";
            return $"{baseUrl}{separator}{_options.OffsetQueryParam}={nextOffset}&{_options.LimitQueryParam}={currentMetadata.PageSize.Value}";
        }

        return null;
    }

    public bool HasMorePages(PaginationMetadata metadata)
    {
        return metadata.HasMore;
    }
}

/// <summary>
/// Configuration options for offset/limit pagination
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
}
