namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Link header pagination strategy (RFC 5988).
/// Parses Link headers to find next/prev/first/last page URLs.
/// Common in RESTful APIs (GitHub, GitLab, etc.)
/// </summary>
/// <remarks>
/// Example Link header:
/// Link: &lt;https://api.example.com/users?page=3&gt;; rel="next",
///       &lt;https://api.example.com/users?page=50&gt;; rel="last"
/// </remarks>
public sealed class LinkHeaderPaginationStrategy : IPaginationStrategy
{
    public string Type => "link-header";

    private readonly LinkHeaderOptions _options;

    public LinkHeaderPaginationStrategy(LinkHeaderOptions? options = null)
    {
        _options = options ?? new LinkHeaderOptions();
    }

    public PaginationMetadata ExtractMetadata(byte[] responseData, IDictionary<string, string>? responseHeaders = null)
    {
        var metadata = new PaginationMetadata();

        if (responseHeaders == null)
        {
            metadata.HasMore = false;
            return metadata;
        }

        // Try to find Link header (case-insensitive)
        var linkHeaderKey = responseHeaders.Keys.FirstOrDefault(k =>
            k.Equals(_options.LinkHeaderName, StringComparison.OrdinalIgnoreCase));

        if (linkHeaderKey == null)
        {
            metadata.HasMore = false;
            return metadata;
        }

        var linkHeader = responseHeaders[linkHeaderKey];
        var links = ParseLinkHeader(linkHeader);

        // Extract next page URL
        if (links.TryGetValue("next", out var nextUrl))
        {
            metadata.NextPageUrl = nextUrl;
            metadata.HasMore = true;
        }
        else
        {
            metadata.HasMore = false;
        }

        // Store other link relations in custom data
        foreach (var (rel, url) in links)
        {
            if (rel != "next")
            {
                metadata.CustomData[$"link_{rel}"] = url;
            }
        }

        return metadata;
    }

    public string? BuildNextPageUrl(string baseUrl, PaginationMetadata currentMetadata)
    {
        // For link-based pagination, the URL is already in the response
        return HasMorePages(currentMetadata) ? currentMetadata.NextPageUrl : null;
    }

    public bool HasMorePages(PaginationMetadata metadata)
    {
        return metadata.HasMore && !string.IsNullOrWhiteSpace(metadata.NextPageUrl);
    }

    /// <summary>
    /// Parse Link header into dictionary of rel -> URL
    /// </summary>
    private Dictionary<string, string> ParseLinkHeader(string linkHeader)
    {
        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Split by comma to get individual links
        var linkParts = linkHeader.Split(',');

        foreach (var part in linkParts)
        {
            var segments = part.Split(';');
            if (segments.Length < 2) continue;

            // Extract URL (between < and >)
            var urlSegment = segments[0].Trim();
            var urlMatch = System.Text.RegularExpressions.Regex.Match(urlSegment, @"<(.+)>");
            if (!urlMatch.Success) continue;
            var url = urlMatch.Groups[1].Value;

            // Extract rel value
            var relSegment = segments.FirstOrDefault(s => s.Trim().StartsWith("rel="));
            if (relSegment == null) continue;

            var relMatch = System.Text.RegularExpressions.Regex.Match(relSegment, @"rel=""?([^""]+)""?");
            if (!relMatch.Success) continue;
            var rel = relMatch.Groups[1].Value;

            links[rel] = url;
        }

        return links;
    }
}

