using KeryxFlux.Domain.Pagination;

namespace KeryxFlux.Application.Services;

/// <summary>
/// Represents a single page response from a paginated API.
/// Contains the raw data, pagination metadata, and HTTP headers.
/// </summary>
public sealed class PagedResponse
{
    public required byte[] Data { get; init; }
    public required PaginationMetadata Metadata { get; init; }
    public Dictionary<string, string>? Headers { get; init; }
    public int PageNumber { get; init; }
}
