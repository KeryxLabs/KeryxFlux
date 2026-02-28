using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainSender = KeryxFlux.Domain.Ports.ISender;

namespace KeryxFlux.Application.Handlers;

/// <summary>
/// Handles model-enhanced message processing where plugins orchestrate
/// sequential calls to model endpoints (AI/ML services).
/// Follows same pattern as ProcessMultiStepCommandHandler but for model invocations.
/// </summary>
public sealed class ProcessModelCommandHandler : IRequestHandler<ProcessModelCommand, ProcessMessageResult>
{
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProcessModelCommandHandler> _logger;
    private readonly IEnumerable<DomainSender> _senders;
    private readonly IHttpClientFactory _httpClientFactory;

    public ProcessModelCommandHandler(
        IDocketManager docketManager,
        IPluginManager pluginManager,
        ILogger<ProcessModelCommandHandler> logger,
        IEnumerable<DomainSender> senders,
        IHttpClientFactory httpClientFactory)
    {
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _logger = logger;
        _senders = senders;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ProcessMessageResult> Handle(ProcessModelCommand request, CancellationToken cancellationToken)
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

            // 3. Verify plugin is a model plugin
            if (pluginResult.Value is not IModelPlugin modelPlugin)
            {
                _logger.LogError(
                    "Plugin {PluginName} does not implement IModelPlugin",
                    pluginResult.Value.Name
                );
                return ProcessMessageResult.Failure("Plugin must implement IModelPlugin for model-enhanced processing");
            }

            // 4. Parse initial message to get model invocation plan
            var context = TransformationContext.Create(
                docketName: request.DocketName,
                receiverType: request.Message.Metadata.TryGetValue("source", out var source) ? source : "unknown",
                correlationId: request.Message.CorrelationId,
                metadata: request.Message.Metadata,
                docketConfiguration: docket.Configuration ?? new Dictionary<string, string>()
            );

            _logger.LogInformation(
                "Parsing initial message for model invocation plan. Docket: {DocketName}",
                request.DocketName
            );

            var plan = modelPlugin.ParseInitialMessage(request.Message.Payload, context);

            // 5. Check if model invocation is needed
            if (plan.FirstStep == null)
            {
                // No model calls needed, forward directly
                _logger.LogInformation(
                    "No model invocation needed for docket {DocketName}. Forwarding directly.",
                    request.DocketName
                );

                if (plan.DirectOutput == null)
                {
                    return ProcessMessageResult.Failure("Plugin returned no model step and no direct output");
                }

                return await ForwardFinalData(
                    plan.DirectOutput,
                    plan.DirectContentType ?? "application/json",
                    docket,
                    request.Message.CorrelationId,
                    cancellationToken
                );
            }

            // 6. Execute model invocation workflow
            return await ProcessModelWorkflow(
                plan.FirstStep,
                modelPlugin,
                docket,
                request.Message.CorrelationId,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing model command for docket {DocketName}", request.DocketName);
            return ProcessMessageResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    private async Task<ProcessMessageResult> ProcessModelWorkflow(
        ModelStep firstStep,
        IModelPlugin modelPlugin,
        Docket docket,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var state = new AccumulatedState();
        var currentStep = firstStep;

        _logger.LogInformation(
            "Starting model workflow for docket {DocketName}. First step: {StepName}",
            docket.Name,
            currentStep.Name
        );

        while (currentStep != null && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "Executing model step {StepName} -> {Endpoint}",
                currentStep.Name,
                currentStep.Endpoint
            );

            // Execute model call
            var modelResponse = await ExecuteModelStep(currentStep, docket, correlationId, cancellationToken);

            // Transform response and get next step
            var stepResult = modelPlugin.TransformModelResponse(currentStep, modelResponse, state);

            // Record step execution
            state.RecordStepExecution(currentStep.Name, currentStep.Endpoint, stepResult.ErrorMessage == null);

            if (stepResult.ErrorMessage != null)
            {
                _logger.LogError(
                    "Model step transformation failed. Step: {StepName}, Error: {ErrorMessage}",
                    currentStep.Name,
                    stepResult.ErrorMessage
                );
                return ProcessMessageResult.Failure($"Step '{currentStep.Name}' failed: {stepResult.ErrorMessage}");
            }

            if (stepResult.IsComplete)
            {
                // Workflow complete - forward final data
                _logger.LogInformation(
                    "Model workflow complete for docket {DocketName} ({TotalSteps} steps executed). Forwarding result.",
                    docket.Name,
                    state.ExecutionHistory.Count
                );

                if (stepResult.FinalData == null)
                {
                    return ProcessMessageResult.Failure("Step marked complete but provided no final data");
                }

                return await ForwardFinalData(
                    stepResult.FinalData,
                    stepResult.ContentType ?? "application/json",
                    docket,
                    correlationId,
                    cancellationToken
                );
            }

            // Continue with next step
            currentStep = stepResult.ContinuationStep;

            if (currentStep != null)
            {
                _logger.LogDebug(
                    "Continuing model workflow with next step: {NextStepName}",
                    currentStep.Name
                );
            }
        }

        _logger.LogWarning("Model workflow completed without forwarding (no Complete() returned)");
        return ProcessMessageResult.Success(0);
    }

    private async Task<byte[]> ExecuteModelStep(
        ModelStep step,
        Docket docket,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromMinutes(5); // Models can take time

        var request = new HttpRequestMessage(new HttpMethod(step.Method), step.Endpoint)
        {
            Content = new ByteArrayContent(step.RequestBody)
        };

        // Add headers
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

    private async Task<ProcessMessageResult> ForwardFinalData(
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

            // Resolve destination URL with path templates
            var destinationUrl = destination.Url ?? destination.Name;
            if (docket.Configuration != null && Domain.Utilities.PathTemplateResolver.HasUnresolvedVariables(destinationUrl))
            {
                destinationUrl = Domain.Utilities.PathTemplateResolver.Resolve(destinationUrl, docket.Configuration);
            }

            // Build headers for protocol-specific destinations
            var headers = new Dictionary<string, string>();

            if (destination.Type.Equals("rabbitmq", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(destination.ConnectionString))
                    headers["connection_string"] = destination.ConnectionString;
                if (!string.IsNullOrEmpty(destination.ExchangeName))
                    headers["exchange_name"] = destination.ExchangeName;
                if (!string.IsNullOrEmpty(destination.RoutingKey))
                    headers["routing_key"] = destination.RoutingKey;
            }
            else if (destination.Type.Equals("kafka", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(destination.BootstrapServers))
                    headers["bootstrap_servers"] = destination.BootstrapServers;
                if (!string.IsNullOrEmpty(destination.Topic))
                    headers["topic"] = destination.Topic;
            }
            else if (destination.Type.Equals("grpc", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(destination.GrpcEndpoint))
                    headers["grpc_endpoint"] = destination.GrpcEndpoint;
                if (!string.IsNullOrEmpty(destination.ServiceName))
                    headers["service_name"] = destination.ServiceName;
            }

            var outboundMessage = new OutboundMessage
            {
                DestinationName = destinationUrl,
                Payload = data,
                ContentType = contentType,
                CorrelationId = correlationId,
                Timeout = TimeSpan.FromSeconds(destination.TimeoutSeconds),
                Headers = headers
            };

            var sendResult = await sender.SendAsync(outboundMessage, cancellationToken);

            if (sendResult.IsSuccess)
            {
                forwardedCount++;
                _logger.LogInformation(
                    "Successfully forwarded model-processed data to {DestinationName}",
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
}
