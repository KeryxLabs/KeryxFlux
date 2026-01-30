namespace KeryxFlux.Domain.Pagination;

/// <summary>
/// Configuration options for link header pagination strategy (RFC 5988).
/// </summary>
public sealed class LinkHeaderOptions
{
    /// <summary>
    /// Name of the Link header (default: "Link")
    /// </summary>
    public string LinkHeaderName { get; set; } = "Link";
}
