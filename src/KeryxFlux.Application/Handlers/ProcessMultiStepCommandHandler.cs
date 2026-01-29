using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using DomainSender = KeryxFlux.Domain.Ports.ISender;

namespace KeryxFlux.Application.Handlers;

/// <summary>
/// Handles multi-step polling workflows where plugins need to make
/// additional requests before producing final output.
/// </summary>
public sealed class ProcessMultiStepCommandHandler : IRequestHandler<ProcessMultiStepCommand, ProcessMessageResult>
{
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProcessMultiStepCommandHandler> _logger;
    private readonly IEnumerable<DomainSender> _senders;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediator _mediator;

    public ProcessMultiStepCommandHandler(
        IDocketManager docketManager,
        IPluginManager pluginManager,
        ILogger<ProcessMultiStepCommandHandler> logger,
        IEnumerable<DomainSender> senders,
        IHttpClientFactory httpClientFactory,
        IMediator mediator)
    {
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _logger = logger;
        _senders = senders;
        _httpClientFactory = httpClientFactory;
        _mediator = mediator;
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

            // 4. Check if this is the initial poll or an item step
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
        var baseUrl = docket.Scheduler?.Server?.Address ?? docket.ServerInformation?.Address;

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
