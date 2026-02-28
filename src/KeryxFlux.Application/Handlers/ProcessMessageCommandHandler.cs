using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using KeryxFlux.Domain.Utilities;
using MediatR;
using Microsoft.Extensions.Logging;
using DomainSender = KeryxFlux.Domain.Ports.ISender;

namespace KeryxFlux.Application.Handlers;

/// <summary>
/// Handles processing of received messages through the transformation pipeline
/// </summary>
public sealed class ProcessMessageCommandHandler : IRequestHandler<ProcessMessageCommand, ProcessMessageResult>
{
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<ProcessMessageCommandHandler> _logger;
    private readonly IEnumerable<DomainSender> _senders;
    private readonly IMediator _mediator;

    public ProcessMessageCommandHandler(
        IDocketManager docketManager,
        IPluginManager pluginManager,
        ILogger<ProcessMessageCommandHandler> logger,
        IEnumerable<DomainSender> senders,
        IMediator mediator)
    {
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _logger = logger;
        _senders = senders;
        _mediator = mediator;
    }

    public async Task<ProcessMessageResult> Handle(ProcessMessageCommand request, CancellationToken cancellationToken)
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

            // 3. Check plugin type and route appropriately
            // Route to model command handler if plugin implements IModelPlugin
            if (plugin is IModelPlugin)
            {
                _logger.LogInformation(
                    "Plugin {PluginName} implements IModelPlugin. Routing to ProcessModelCommandHandler.",
                    plugin.Name
                );

                var modelCommand = new ProcessModelCommand
                {
                    DocketName = request.DocketName,
                    Message = request.Message
                };

                return await _mediator.Send(modelCommand, cancellationToken);
            }

            // Standard receiver plugin flow
            if (plugin is not IReceiverPlugin receiverPlugin)
            {
                _logger.LogError(
                    "Plugin {PluginName} does not implement IReceiverPlugin or IModelPlugin (implements: {PluginType})",
                    plugin.Name,
                    plugin.GetType().Name);
                return ProcessMessageResult.Failure(
                    $"Plugin must implement IReceiverPlugin or IModelPlugin for receiver-type dockets");
            }

            // 4. Create transformation context with docket configuration
            var context = TransformationContext.Create(
                docketName: request.DocketName,
                receiverType: docket.Type.ToString().ToLowerInvariant(),
                correlationId: request.Message.CorrelationId,
                metadata: request.Message.Metadata,
                docketConfiguration: docket.Configuration
            );

            // 5. Execute transformation
            _logger.LogInformation(
                "Transforming message for docket {DocketName} using plugin {PluginName}",
                request.DocketName,
                plugin.Name
            );

            var transformResult = receiverPlugin.Transform(request.Message.Payload, context);

            if (!transformResult.IsSuccess)
            {
                _logger.LogError(
                    "Transformation failed for docket {DocketName}: {ErrorMessage}",
                    request.DocketName,
                    transformResult.ErrorMessage
                );
                return ProcessMessageResult.Failure($"Transformation failed: {transformResult.ErrorMessage}");
            }

            // 6. Forward to all configured destinations
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

                // Resolve path template variables in destination URL
                var destinationUrl = destination.Url ?? destination.Name;
                if (docket.Configuration != null && PathTemplateResolver.HasUnresolvedVariables(destinationUrl))
                {
                    destinationUrl = PathTemplateResolver.Resolve(destinationUrl, docket.Configuration);
                    
                    _logger.LogDebug(
                        "Resolved destination URL template: {Original} -> {Resolved}",
                        destination.Url,
                        destinationUrl
                    );
                }

                // Build headers with destination-specific configuration
                var headers = new Dictionary<string, string>();

                // Add RabbitMQ-specific headers
                if (destination.Type.Equals("rabbitmq", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(destination.ConnectionString))
                        headers["connection_string"] = destination.ConnectionString;
                    
                    if (!string.IsNullOrEmpty(destination.ExchangeName))
                        headers["exchange_name"] = destination.ExchangeName;
                    
                    
                    if (!string.IsNullOrEmpty(destination.RoutingKey))
                        headers["routing_key"] = destination.RoutingKey;
                    
                    if (!string.IsNullOrEmpty(destination.ExchangeType))
                        headers["exchange_type"] = destination.ExchangeType;
                    
                    if (destination.Durable.HasValue)
                        headers["durable"] = destination.Durable.Value.ToString();
                }
                // Add Kafka-specific headers
                else if (destination.Type.Equals("kafka", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(destination.BootstrapServers))
                        headers["bootstrap_servers"] = destination.BootstrapServers;
                    
                    if (!string.IsNullOrEmpty(destination.Topic))
                        headers["topic"] = destination.Topic;
                    
                    if (!string.IsNullOrEmpty(destination.PartitionKey))
                        headers["partition_key"] = destination.PartitionKey;
                }
                // Add gRPC-specific headers
                else if (destination.Type.Equals("grpc", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(destination.GrpcEndpoint))
                        headers["grpc_endpoint"] = destination.GrpcEndpoint;
                    
                    if (!string.IsNullOrEmpty(destination.ServiceName))
                        headers["service_name"] = destination.ServiceName;
                    
                    if (!string.IsNullOrEmpty(destination.MethodName))
                        headers["method_name"] = destination.MethodName;
                    
                    if (destination.UseTls.HasValue)
                        headers["use_tls"] = destination.UseTls.Value.ToString();
                }
                // Add TCP-specific headers
                else if (destination.Type.Equals("tcp", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(destination.Host))
                        headers["host"] = destination.Host;
                    
                    if (destination.Port.HasValue)
                        headers["port"] = destination.Port.Value.ToString();
                    
                    if (!string.IsNullOrEmpty(destination.Framing))
                        headers["framing"] = destination.Framing;
                    
                    if (!string.IsNullOrEmpty(destination.Delimiter))
                        headers["delimiter"] = destination.Delimiter;
                    
                    if (destination.Persistent.HasValue)
                        headers["persistent"] = destination.Persistent.Value.ToString();
                }

                var outboundMessage = new OutboundMessage
                {
                    DestinationName = destinationUrl,
                    Payload = transformResult.Data!,
                    ContentType = transformResult.ContentType ?? "application/octet-stream",
                    CorrelationId = request.Message.CorrelationId,
                    Timeout = TimeSpan.FromSeconds(destination.TimeoutSeconds),
                    Headers = headers
                };

                var sendResult = await sender.SendAsync(outboundMessage, cancellationToken);

                if (sendResult.IsSuccess)
                {
                    forwardedCount++;
                    _logger.LogInformation(
                        "Successfully forwarded message to {DestinationName} ({DestinationType})",
                        destination.Name,
                        destination.Type
                    );
                }
                else
                {
                    _logger.LogError(
                        "Failed to forward message to {DestinationName}: {ErrorMessage}",
                        destination.Name,
                        sendResult.ErrorMessage
                    );
                }
            }

            return ProcessMessageResult.Success(forwardedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing message for docket {DocketName}", request.DocketName);
            return ProcessMessageResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
}
