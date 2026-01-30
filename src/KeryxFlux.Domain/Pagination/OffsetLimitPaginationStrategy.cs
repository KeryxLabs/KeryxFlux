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

        // Check if using path-based pagination (URL contains {page} or {offset} placeholders)
        if (_options.UsePathParameters && (baseUrl.Contains("{page}") || baseUrl.Contains("{offset}")))
        {
            return BuildPathBasedUrl(baseUrl, currentMetadata);
        }

        // Default: Query parameter based
        return BuildQueryBasedUrl(baseUrl, currentMetadata);
    }

    /// <summary>
    /// Build URL with path parameters: /api/data/{page}/{size}
    /// </summary>
    private string BuildPathBasedUrl(string baseUrl, PaginationMetadata currentMetadata)
    {
        var url = baseUrl;

        // Page-based path parameters
        if (currentMetadata.CurrentPage.HasValue && currentMetadata.PageSize.HasValue)
        {
            var nextPage = currentMetadata.CurrentPage.Value + 1;
            
            // Replace {page} or custom placeholder
            url = url.Replace($"{{{_options.PagePathPlaceholder}}}", nextPage.ToString());
            
            // Replace {size} or custom placeholder  
            url = url.Replace($"{{{_options.PageSizePathPlaceholder}}}", currentMetadata.PageSize.Value.ToString());
        }
        // Offset-based path parameters
        else if (currentMetadata.Offset.HasValue && currentMetadata.PageSize.HasValue)
        {
            var nextOffset = currentMetadata.Offset.Value + currentMetadata.PageSize.Value;
            
            url = url.Replace($"{{{_options.OffsetPathPlaceholder}}}", nextOffset.ToString());
            url = url.Replace($"{{{_options.LimitPathPlaceholder}}}", currentMetadata.PageSize.Value.ToString());
        }

        return url;
    }

    /// <summary>
    /// Build URL with query parameters: /api/data?page=1&size=100
    /// </summary>
    private string BuildQueryBasedUrl(string baseUrl, PaginationMetadata currentMetadata)
    {
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


