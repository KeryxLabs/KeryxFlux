using KeryxFlux.Application.Commands;
using KeryxFlux.Application.Services;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Pagination;
using KeryxFlux.Domain.Ports;
using KeryxFlux.Domain.Utilities;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using DomainSender = KeryxFlux.Domain.Ports.ISender;

namespace KeryxFlux.Application.Handlers;

/// <summary>
/// Handles multi-step polling workflows where plugins need to make
/// additional requests before producing final output.
/// Supports both item-level parallelism and paginated API polling.
/// </summary>
public sealed class ProcessMultiStepCommandHandler : IRequestHandler<ProcessMultiStepCommand, ProcessMessageResult>
{
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProcessMultiStepCommandHandler> _logger;
    private readonly IEnumerable<DomainSender> _senders;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediator _mediator;
    private readonly PaginationOrchestrator _paginationOrchestrator;

    public ProcessMultiStepCommandHandler(
        IDocketManager docketManager,
        IPluginManager pluginManager,
        ILogger<ProcessMultiStepCommandHandler> logger,
        IEnumerable<DomainSender> senders,
        IHttpClientFactory httpClientFactory,
        IMediator mediator,
        PaginationOrchestrator paginationOrchestrator)
    {
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _logger = logger;
        _senders = senders;
        _httpClientFactory = httpClientFactory;
        _mediator = mediator;
        _paginationOrchestrator = paginationOrchestrator;
    }

    public async Task<ProcessMessageResult> Handle(ProcessMultiStepCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Get the docket configuration
            var docket = _docketManager.GetDocketByName(request.DocketName);
            if (docket == null)
            {
                _logger.LogError("Docket {DocketName} not found", request.DocketName);
                return ProcessMessageResult.Failure($"Docket '{request.DocketName}' not found");
            }

            // 2. Load the plugin
            var pluginResult = _pluginManager.LoadPlugin(docket.PluginLocation);
            if (!pluginResult.IsSuccess || pluginResult.Value == null)
            {
                _logger.LogError("Failed to load plugin from {PluginLocation}", docket.PluginLocation);
                return ProcessMessageResult.Failure($"Failed to load plugin: {pluginResult.Error}");
            }

            var plugin = pluginResult.Value;

            // 3. Verify plugin is a poller plugin
            if (plugin is not IPollerPlugin pollerPlugin)
            {
                _logger.LogError(
                    "Plugin {PluginName} does not implement IPollerPlugin (implements: {PluginType})",
                    plugin.Name,
                    plugin.GetType().Name);
                return ProcessMessageResult.Failure(
                    $"Plugin must implement IPollerPlugin for poller-type dockets");
            }

            // 4. Check if docket has pagination configured
            if (docket.Scheduler?.Pagination != null)
            {
                // Use pagination flow
                return await ProcessPaginatedPoll(pollerPlugin, docket, cancellationToken);
            }

            // 5. Legacy flow: Check if this is the initial poll or an item step
            var isInitialPoll = request.AccumulatedState == null && string.IsNullOrEmpty(request.CurrentStepName);

            if (isInitialPoll)
            {
                // PHASE 1: Parse initial response to extract items
                return await ProcessInitialPoll(pollerPlugin, request, docket, cancellationToken);
            }
            else
            {
                // PHASE 2: Process a step for a specific item
                return await ProcessItemStep(pollerPlugin, request, docket, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing multi-step command for docket {DocketName}", request.DocketName);
            return ProcessMessageResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Process a paginated API poll using configured pagination strategy.
    /// Processes each page individually and calls the plugin for each page.
    /// </summary>
    private async Task<ProcessMessageResult> ProcessPaginatedPoll(
        IPollerPlugin pollerPlugin,
        Docket docket,
        CancellationToken cancellationToken)
    {
        var paginationConfig = docket.Scheduler!.Pagination!;

        _logger.LogInformation(
            "Starting paginated poll for docket {DocketName} using {Strategy} strategy",
            docket.Name,
            paginationConfig.Strategy
        );

        // Get pagination strategy
        var strategy = _paginationOrchestrator.GetStrategy(paginationConfig.Strategy);
        if (strategy == null)
        {
            _logger.LogError("Unknown pagination strategy: {Strategy}", paginationConfig.Strategy);
            return ProcessMessageResult.Failure($"Unknown pagination strategy: {paginationConfig.Strategy}");
        }

        // Build base URL with path template resolution
        var baseUrl = docket.Scheduler.Server.Address;
        if (docket.Configuration != null && PathTemplateResolver.HasUnresolvedVariables(baseUrl))
        {
            baseUrl = PathTemplateResolver.Resolve(baseUrl, docket.Configuration);
            _logger.LogDebug("Resolved server address: {Url}", baseUrl);
        }

        // Add initial page size parameter
        var separator = baseUrl.Contains('?') ? "&" : "?";
        var currentUrl = $"{baseUrl}{separator}size={paginationConfig.PageSize}";

        var pageCount = 0;
        var totalForwardedCount = 0;
        PaginationMetadata? previousMetadata = null;

        // Process pages one-by-one
        while (currentUrl != null && !cancellationToken.IsCancellationRequested)
        {
            // Safety check
            if (paginationConfig.MaxPages > 0 && pageCount >= paginationConfig.MaxPages)
            {
                _logger.LogWarning(
                    "Reached maximum page limit ({MaxPages}) for docket {DocketName}. Stopping pagination.",
                    paginationConfig.MaxPages,
                    docket.Name
                );
                break;
            }

            _logger.LogDebug("Fetching page {PageNumber}: {Url}", pageCount + 1, currentUrl);

            // Fetch current page
            var pageResponse = await FetchPageAsync(currentUrl, docket, cancellationToken);
            
            // Extract pagination metadata from this page
            var currentMetadata = strategy.ExtractMetadata(pageResponse.Data, pageResponse.Headers);
            pageCount++;

            _logger.LogInformation(
                "Fetched page {PageNumber} ({ItemCount} items) for docket {DocketName}",
                pageCount,
                currentMetadata.CurrentPageItemCount ?? 0,
                docket.Name
            );

            // Determine if this is the last page
            var isLastPage = !strategy.HasMorePages(currentMetadata);

            // Create pagination context for the plugin
            var paginationContext = new PaginationContext
            {
                CurrentPage = pageCount,
                TotalPages = currentMetadata.TotalPages,
                TotalItems = currentMetadata.TotalCount,
                PageSize = currentMetadata.PageSize ?? paginationConfig.PageSize,
                Strategy = paginationConfig.Strategy,
                IsFirstPage = pageCount == 1,
                IsLastPage = isLastPage,
                CurrentCursor = previousMetadata?.NextCursor,
                NextCursor = currentMetadata.NextCursor,
                AdditionalData = new Dictionary<string, string>
                {
                    { "offset", currentMetadata.Offset?.ToString() ?? "0" },
                    { "has_more", currentMetadata.HasMore.ToString() }
                }
            };

            // Create transformation context with pagination info
            var context = TransformationContext.Create(
                docketName: docket.Name,
                receiverType: "poller-paginated",
                correlationId: Guid.NewGuid().ToString(),
                metadata: new Dictionary<string, string>
                {
                    { "page_number", pageCount.ToString() },
                    { "strategy", paginationConfig.Strategy },
                    { "is_last_page", isLastPage.ToString() }
                },
                docketConfiguration: docket.Configuration,
                pagination: paginationContext
            );

            // Transform this single page
            if (pollerPlugin is not IReceiverPlugin receiverPlugin)
            {
                _logger.LogError("Plugin does not implement IReceiverPlugin for transformation");
                return ProcessMessageResult.Failure("Plugin must implement IReceiverPlugin");
            }

            var transformResult = receiverPlugin.Transform(pageResponse.Data, context);

            if (!transformResult.IsSuccess)
            {
                _logger.LogError(
                    "Transformation failed for page {PageNumber}: {ErrorMessage}",
                    pageCount,
                    transformResult.ErrorMessage
                );
                
                // Option: Continue to next page or fail completely?
                // For now, fail the whole operation
                return ProcessMessageResult.Failure($"Page {pageCount} transformation failed: {transformResult.ErrorMessage}");
            }

            // Forward this page's transformed data
            var pageForwardCount = await ForwardTransformedData(
                transformResult, 
                docket, 
                context.CorrelationId, 
                cancellationToken
            );

            if (pageForwardCount.IsSuccess)
            {
                totalForwardedCount += pageForwardCount.DestinationsForwarded;
            }

            // Check if more pages exist
            if (isLastPage)
            {
                _logger.LogInformation(
                    "Pagination complete for docket {DocketName}. Processed {TotalPages} pages, forwarded {TotalMessages} messages.",
                    docket.Name,
                    pageCount,
                    totalForwardedCount
                );
                break;
            }

            // Build next page URL
            currentUrl = strategy.BuildNextPageUrl(baseUrl, currentMetadata);
            previousMetadata = currentMetadata;

            if (currentUrl == null)
            {
                _logger.LogWarning("Strategy indicated more pages but failed to build next URL. Stopping.");
                break;
            }
        }

        return ProcessMessageResult.Success(totalForwardedCount);
    }

    /// <summary>
    /// Fetch a single page from the API
    /// </summary>
    private async Task<PagedResponse> FetchPageAsync(
        string url,
        Docket docket,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(docket.Scheduler!.Server.TimeoutSeconds);

        _logger.LogDebug("Fetching page: {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Add authentication if configured
        if (docket.Scheduler.Server.Authentication != null)
        {
            var auth = docket.Scheduler.Server.Authentication;
            
            // Get token from environment variable
            string? token = null;
            if (!string.IsNullOrEmpty(auth.SecretEnv))
            {
                token = Environment.GetEnvironmentVariable(auth.SecretEnv);
            }

            if (!string.IsNullOrEmpty(token))
            {
                if (auth.Type.Equals("bearer", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                else if (auth.Type.Equals("token", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.Add("Authorization", $"token {token}");
                }
                else if (auth.Type.Equals("api_key", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(auth.Header))
                {
                    request.Headers.Add(auth.Header, token);
                }
            }
        }

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        // Extract headers for pagination (needed by link-header strategy)
        var headers = response.Headers.ToDictionary(
            h => h.Key,
            h => string.Join(",", h.Value),
            StringComparer.OrdinalIgnoreCase
        );

        return new PagedResponse
        {
            Data = data,
            Metadata = new PaginationMetadata(), // Will be filled by strategy
            Headers = headers,
            PageNumber = 0 // Will be incremented by orchestrator
        };
    }

    /// <summary>
    /// Forward transformed data to all destinations
    /// </summary>
    private async Task<ProcessMessageResult> ForwardTransformedData(
        TransformationResult transformResult,
        Docket docket,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var forwardedCount = 0;

        foreach (var destination in docket.Forwarding.Destinations)
        {
            var sender = _senders.FirstOrDefault(s => s.Type == destination.Type);
            if (sender == null)
            {
                _logger.LogWarning(
                    "No sender found for type {DestinationType}",
                    destination.Type
                );
                continue;
            }

            // Resolve destination URL with path templates
            var destinationUrl = destination.Url ?? destination.Name;
            if (docket.Configuration != null && PathTemplateResolver.HasUnresolvedVariables(destinationUrl))
            {
                destinationUrl = PathTemplateResolver.Resolve(destinationUrl, docket.Configuration);
            }

            var outboundMessage = new OutboundMessage
            {
                DestinationName = destinationUrl,
                Payload = transformResult.Data!,
                ContentType = transformResult.ContentType ?? "application/json",
                CorrelationId = correlationId,
                Timeout = TimeSpan.FromSeconds(destination.TimeoutSeconds)
            };

            var sendResult = await sender.SendAsync(outboundMessage, cancellationToken);

            if (sendResult.IsSuccess)
            {
                forwardedCount++;
                _logger.LogInformation(
                    "Successfully forwarded paginated data to {DestinationName}",
                    destination.Name
                );
            }
            else
            {
                _logger.LogError(
                    "Failed to forward to {DestinationName}: {ErrorMessage}",
                    destination.Name,
                    sendResult.ErrorMessage
                );
            }
        }

        return ProcessMessageResult.Success(forwardedCount);
    }

    private async Task<ProcessMessageResult> ProcessInitialPoll(
        IPollerPlugin pollerPlugin,
        ProcessMultiStepCommand request,
        Docket docket,
        CancellationToken cancellationToken)

    {
        var context = TransformationContext.Create(
            docketName: request.DocketName,
            receiverType: "poller",
            correlationId: request.Message.CorrelationId,
            metadata: request.Message.Metadata
        );

        _logger.LogInformation(
            "Parsing initial poll response for docket {DocketName}",
            request.DocketName
        );

        // Parse initial response to extract items
        var parseResult = pollerPlugin.ParseInitialResponse(request.Message.Payload, context);

        if (!parseResult.IsSuccess)
        {
            _logger.LogError(
                "Failed to parse initial response for docket {DocketName}: {ErrorMessage}",
                request.DocketName,
                parseResult.ErrorMessage
            );
            return ProcessMessageResult.Failure($"Parse failed: {parseResult.ErrorMessage}");
        }

        if (parseResult.Items.Count == 0)
        {
            _logger.LogInformation(
                "No items found in initial poll for docket {DocketName}",
                request.DocketName
            );
            return ProcessMessageResult.Success(0);
        }

        _logger.LogInformation(
            "Extracted {ItemCount} items from initial poll for docket {DocketName}. Processing each independently in parallel.",
            parseResult.Items.Count,
            request.DocketName
        );

        // Process all items in parallel (each item is an independent workflow)
        // Use ConcurrentBag for thread-safe adds without locking
        var forwardedCounts = new ConcurrentBag<int>();

        await Parallel.ForEachAsync(
            parseResult.Items,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount * 2,
                CancellationToken = cancellationToken
            },
            async (item, ct) =>
            {
                // Use Result pattern instead of try/catch for flow control
                Result<int> result = await ProcessItemWorkflow(
                    item,
                    pollerPlugin,
                    docket,
                    request.Message.CorrelationId,
                    ct);

                if (result.IsSuccess)
                {
                    forwardedCounts.Add(result.Value);
                }
                else
                {
                    _logger.LogError(
                        "Failed to process item {ItemId} for docket {DocketName}: {ErrorCode} - {ErrorDetails}. Continuing with other items.",
                        item.ItemId,
                        request.DocketName,
                        result.Error.Code,
                        result.Error.Details
                    );
                }
            }
        );

        var totalForwarded = forwardedCounts.Sum();
        
        _logger.LogInformation(
            "Completed processing {TotalItems} items for docket {DocketName}. {ForwardedCount} messages forwarded successfully.",
            parseResult.Items.Count,
            request.DocketName,
            totalForwarded
        );

        return ProcessMessageResult.Success(totalForwarded);
    }

    private async Task<Result<int>> ProcessItemWorkflow(
        PollingItem item,
        IPollerPlugin pollerPlugin,
        Docket docket,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting workflow for item {ItemId} in docket {DocketName}",
            item.ItemId,
            docket.Name
        );

        // Each item gets its own state
        var itemState = new AccumulatedState();
        itemState.Add("item", item.ItemData);
        itemState.Add("itemId", item.ItemId);

        // Start with the initial step
        var currentStep = item.InitialStep;

        while (currentStep != null)
        {
            try
            {
                _logger.LogInformation(
                    "Executing step {StepName} for item {ItemId} -> {RequestUrl}",
                    currentStep.Name,
                    item.ItemId,
                    currentStep.RequestUrl
                );

                // Make HTTP request for this step
                var stepResponse = await ExecuteStepRequest(currentStep, docket, correlationId, cancellationToken);

                // Transform this step - plugin decides what's next
                var stepResult = pollerPlugin.TransformItemStep(
                    currentStep,
                    stepResponse,
                    itemState,
                    item.ItemId
                );

                // Record step execution
                itemState.RecordStepExecution(currentStep.Name, currentStep.RequestUrl, stepResult.IsSuccess);

                if (!stepResult.IsSuccess)
                {
                    _logger.LogError(
                        "Step transformation failed for item {ItemId}, step {StepName}: {ErrorMessage}",
                        item.ItemId,
                        currentStep.Name,
                        stepResult.ErrorMessage
                    );
                    return Result.Failure<int>(new Error("StepTransformationFailed", stepResult.ErrorMessage));
                }

                if (stepResult.IsComplete)
                {
                    // This item is complete - forward its message
                    _logger.LogInformation(
                        "Item {ItemId} workflow complete ({TotalSteps} steps executed). Forwarding message.",
                        item.ItemId,
                        itemState.ExecutionHistory.Count
                    );

                    var forwardedCount = await ForwardFinalData(
                        stepResult.FinalData!,
                        stepResult.ContentType!,
                        docket,
                        $"{correlationId}-{item.ItemId}",
                        cancellationToken
                    );

                    return Result.Success(forwardedCount);
                }

                // Continue with next step
                currentStep = stepResult.ContinuationStep;

                if (currentStep != null)
                {
                    _logger.LogDebug(
                        "Item {ItemId} continuing with next step: {NextStepName}",
                        item.ItemId,
                        currentStep.Name
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute step {StepName} for item {ItemId}", currentStep.Name, item.ItemId);
                throw;
            }
        }

        _logger.LogWarning("Item {ItemId} completed without forwarding (no Complete() returned)", item.ItemId);
        return 0;
    }

    private async Task<byte[]> ExecuteStepRequest(
        NextStep step,
        Docket docket,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var baseUrl = docket.Scheduler?.Server?.Address;

        // Build full URL
        var fullUrl = step.RequestUrl.StartsWith("http")
            ? step.RequestUrl
            : $"{baseUrl?.TrimEnd('/')}/{step.RequestUrl.TrimStart('/')}";

        // Make HTTP request
        var request = new HttpRequestMessage(new HttpMethod(step.Method), fullUrl);

        if (step.RequestBody != null)
        {
            request.Content = new ByteArrayContent(step.RequestBody);
        }

        if (step.Headers != null)
        {
            foreach (var header in step.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private Task<ProcessMessageResult> ProcessItemStep(
        IPollerPlugin pollerPlugin,
        ProcessMultiStepCommand request,
        Docket docket,
        CancellationToken cancellationToken)
    {
        // This path is for backward compatibility or edge cases
        // In the new design, items are processed in ProcessItemWorkflow
        _logger.LogWarning("ProcessItemStep called unexpectedly for docket {DocketName}", request.DocketName);
        return Task.FromResult(ProcessMessageResult.Failure("Invalid state: item step called without item context"));
    }

    private async Task<int> ForwardFinalData(
        byte[] data,
        string contentType,
        Docket docket,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var forwardedCount = 0;

        foreach (var destination in docket.Forwarding.Destinations)
        {
            var sender = _senders.FirstOrDefault(s => s.Type == destination.Type);
            if (sender == null)
            {
                _logger.LogWarning(
                    "No sender found for type {DestinationType}, skipping destination {DestinationName}",
                    destination.Type,
                    destination.Name
                );
                continue;
            }

            var outboundMessage = new OutboundMessage
            {
                DestinationName = destination.Name,
                Payload = data,
                ContentType = contentType,
                CorrelationId = correlationId,
                Timeout = TimeSpan.FromSeconds(destination.TimeoutSeconds)
            };

            var sendResult = await sender.SendAsync(outboundMessage, cancellationToken);

            if (sendResult.IsSuccess)
            {
                forwardedCount++;
                _logger.LogInformation(
                    "Successfully forwarded final data to {DestinationName}",
                    destination.Name
                );
            }
            else
            {
                _logger.LogError(
                    "Failed to forward to {DestinationName}: {ErrorMessage}",
                    destination.Name,
                    sendResult.ErrorMessage
                );
            }
        }

        return forwardedCount;
    }
}
