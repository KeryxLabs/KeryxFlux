using KeryxFlux.Domain.Pagination;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Application.Services;

/// <summary>
/// Orchestrates paginated API polling using configured pagination strategies
/// </summary>
public sealed class PaginationOrchestrator
{
    private readonly ILogger<PaginationOrchestrator> _logger;
    private readonly Dictionary<string, IPaginationStrategy> _strategies;

    public PaginationOrchestrator(ILogger<PaginationOrchestrator> logger)
    {
        _logger = logger;
        _strategies = new Dictionary<string, IPaginationStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            ["offset-limit"] = new OffsetLimitPaginationStrategy(),
            ["cursor"] = new CursorPaginationStrategy(),
            ["link-header"] = new LinkHeaderPaginationStrategy()
        };
    }

    /// <summary>
    /// Get pagination strategy by name
    /// </summary>
    public IPaginationStrategy? GetStrategy(string strategyType)
    {
        return _strategies.GetValueOrDefault(strategyType);
    }

    /// <summary>
    /// Register a custom pagination strategy
    /// </summary>
    public void RegisterStrategy(IPaginationStrategy strategy)
    {
        _strategies[strategy.Type] = strategy;
        _logger.LogInformation("Registered pagination strategy: {StrategyType}", strategy.Type);
    }

    /// <summary>
    /// Execute paginated fetch operation
    /// </summary>
    /// <param name="baseUrl">Base URL for the API endpoint</param>
    /// <param name="strategy">Pagination strategy to use</param>
    /// <param name="maxPages">Maximum pages to fetch (0 = unlimited)</param>
    /// <param name="fetchPage">Function to fetch a single page</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all page responses</returns>
    public async Task<List<PagedResponse>> FetchAllPagesAsync(
        string baseUrl,
        IPaginationStrategy strategy,
        int maxPages,
        Func<string, CancellationToken, Task<PagedResponse>> fetchPage,
        CancellationToken cancellationToken)
    {
        var allPages = new List<PagedResponse>();
        var currentUrl = baseUrl;
        var pageCount = 0;

        _logger.LogInformation(
            "Starting paginated fetch with strategy {Strategy} (max pages: {MaxPages})",
            strategy.Type,
            maxPages > 0 ? maxPages : "unlimited"
        );

        while (currentUrl != null)
        {
            // Safety check for max pages
            if (maxPages > 0 && pageCount >= maxPages)
            {
                _logger.LogWarning(
                    "Reached maximum page limit ({MaxPages}). Stopping pagination.",
                    maxPages
                );
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug("Fetching page {PageNumber}: {Url}", pageCount + 1, currentUrl);

            // Fetch current page
            var pageResponse = await fetchPage(currentUrl, cancellationToken);
            allPages.Add(pageResponse);
            pageCount++;

            _logger.LogInformation(
                "Fetched page {PageNumber} ({ItemCount} items)",
                pageCount,
                pageResponse.Metadata.CurrentPageItemCount ?? 0
            );

            // Extract pagination metadata and build next URL
            var metadata = strategy.ExtractMetadata(pageResponse.Data, pageResponse.Headers);
            
            if (!strategy.HasMorePages(metadata))
            {
                _logger.LogInformation(
                    "Pagination complete. Fetched {TotalPages} pages.",
                    pageCount
                );
                break;
            }

            currentUrl = strategy.BuildNextPageUrl(baseUrl, metadata);

            if (currentUrl == null)
            {
                _logger.LogWarning("Strategy indicated more pages but failed to build next URL. Stopping.");
                break;
            }
        }

        return allPages;
    }
}

