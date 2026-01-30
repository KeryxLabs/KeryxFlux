using System.Text.Json;

namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Cursor-based pagination strategy.
/// Uses tokens/cursors for navigation (common in modern APIs like GraphQL, Twitter, etc.)
/// </summary>
/// <remarks>
/// Example API response:
/// {
///   "data": [...],
///   "paging": {
///     "next": "cursor_token_abc123",
///     "has_more": true
///   }
/// }
/// </remarks>
public sealed class CursorPaginationStrategy : IPaginationStrategy
{
    public string Type => "cursor";

    private readonly CursorPaginationOptions _options;

    public CursorPaginationStrategy(CursorPaginationOptions? options = null)
    {
        _options = options ?? new CursorPaginationOptions();
    }

    public PaginationMetadata ExtractMetadata(byte[] responseData, IDictionary<string, string>? responseHeaders = null)
    {
        var metadata = new PaginationMetadata();

        try
        {
            using var doc = JsonDocument.Parse(responseData);
            var root = doc.RootElement;

            // Navigate to paging object (if nested)
            JsonElement pagingElement = root;
            if (!string.IsNullOrEmpty(_options.PagingObjectPath))
            {
                var pathParts = _options.PagingObjectPath.Split('.');
                foreach (var part in pathParts)
                {
                    if (pagingElement.TryGetProperty(part, out var nested))
                    {
                        pagingElement = nested;
                    }
                    else
                    {
                        // Path not found, assume no paging info
                        metadata.HasMore = false;
                        return metadata;
                    }
                }
            }

            // Extract next cursor
            if (pagingElement.TryGetProperty(_options.NextCursorFieldName, out var cursorElement))
            {
                metadata.NextCursor = cursorElement.GetString();
            }

            // Check for explicit "has_more" field
            if (pagingElement.TryGetProperty(_options.HasMoreFieldName, out var hasMoreElement))
            {
                metadata.HasMore = hasMoreElement.GetBoolean();
            }
            else
            {
                // If no explicit has_more field, assume has more if cursor exists
                metadata.HasMore = !string.IsNullOrWhiteSpace(metadata.NextCursor);
            }

            // Optional: extract current page size
            if (pagingElement.TryGetProperty(_options.PageSizeFieldName, out var sizeElement))
            {
                metadata.PageSize = sizeElement.GetInt32();
            }
        }
        catch (JsonException)
        {
            metadata.HasMore = false;
        }

        return metadata;
    }

    public string? BuildNextPageUrl(string baseUrl, PaginationMetadata currentMetadata)
    {
        if (!HasMorePages(currentMetadata) || string.IsNullOrWhiteSpace(currentMetadata.NextCursor))
        {
            return null;
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}{_options.CursorQueryParam}={Uri.EscapeDataString(currentMetadata.NextCursor)}";
    }

    public bool HasMorePages(PaginationMetadata metadata)
    {
        return metadata.HasMore && !string.IsNullOrWhiteSpace(metadata.NextCursor);
    }
}

