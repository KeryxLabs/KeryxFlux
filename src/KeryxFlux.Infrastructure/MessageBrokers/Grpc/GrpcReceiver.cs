using Google.Protobuf;
using Grpc.Core;
using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KeryxFlux.Infrastructure.MessageBrokers.Grpc;

/// <summary>
/// Service for managing gRPC receivers for receiver dockets.
/// Dynamically registers gRPC service endpoints as dockets are loaded/unloaded.
/// Similar to RabbitMqReceiver/KafkaConsumer but for gRPC.
/// </summary>
public class GrpcReceiver : IGrpcReceiverService
{
    private readonly ILogger<GrpcReceiver> _logger;
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly IMediator _mediator;
    
    // Track active receivers: docketName ? serviceName
    private readonly ConcurrentDictionary<string, string> _activeReceivers = new();

    public GrpcReceiver(
        ILogger<GrpcReceiver> logger,
        IDocketManager docketManager,
        IPluginManager pluginManager,
        IMediator mediator)
    {
        _logger = logger;
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _mediator = mediator;
    }

    public async Task RegisterReceiverForDocketAsync(Docket docket)
    {
        if (docket.Receiver == null)
        {
            throw new InvalidOperationException($"Docket {docket.Name} has no receiver configuration");
        }

        var config = docket.Receiver;

        if (string.IsNullOrEmpty(config.GrpcEndpoint))
        {
            throw new InvalidOperationException($"gRPC grpc_endpoint is required for docket {docket.Name}");
        }

        if (string.IsNullOrEmpty(config.ServiceName))
        {
            throw new InvalidOperationException($"gRPC service_name is required for docket {docket.Name}");
        }

        _logger.LogInformation(
            "Registering gRPC receiver for docket: {DocketName}, service: {ServiceName}, endpoint: {Endpoint}",
            docket.Name,
            config.ServiceName,
            config.GrpcEndpoint);

        // Track the receiver
        _activeReceivers[docket.Name] = config.ServiceName;

        _logger.LogInformation(
            "gRPC receiver registered for docket: {DocketName}. Note: gRPC endpoints are registered via ASP.NET Core routing.",
            docket.Name);

        await Task.CompletedTask;
    }

    public async Task UnregisterReceiverForDocketAsync(string docketName)
    {
        if (!_activeReceivers.TryRemove(docketName, out var serviceName))
        {
            _logger.LogWarning("No active gRPC receiver found for docket: {DocketName}", docketName);
            return;
        }

        _logger.LogInformation("Unregistered gRPC receiver for docket: {DocketName}, service: {ServiceName}", 
            docketName, serviceName);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Handle incoming gRPC unary call.
    /// Called by gRPC service implementation.
    /// </summary>
    public async Task<byte[]> HandleUnaryCallAsync(string docketName, byte[] payload, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation(
                "Received gRPC unary call for docket: {DocketName}, size: {Size} bytes",
                docketName,
                payload.Length);

            // Get the docket
            if (!_docketManager.TryGetDocket(docketName, out var docket))
            {
                _logger.LogError("Docket not found: {DocketName}", docketName);
                throw new RpcException(new Status(StatusCode.NotFound, $"Docket '{docketName}' not found"));
            }

            // Load plugin
            var pluginResult = _pluginManager.LoadPlugin(docket.PluginLocation);
            if (!pluginResult.IsSuccess || pluginResult.Value is not IReceiverPlugin plugin)
            {
                _logger.LogError("Failed to load receiver plugin for docket {DocketName}: {Error}", 
                    docketName, pluginResult.Error);
                throw new RpcException(new Status(StatusCode.Internal, $"Plugin load failed: {pluginResult.Error}"));
            }

            // Create ReceivedMessage
            var message = new ReceivedMessage
            {
                Payload = payload,
                ContentType = "application/octet-stream",
                CorrelationId = context.RequestHeaders.GetValue("x-correlation-id") ?? Guid.NewGuid().ToString(),
                SourceEndpoint = context.Method,
                Metadata = new Dictionary<string, string>
                {
                    { "source", "grpc" },
                    { "method", context.Method },
                    { "peer", context.Peer }
                }
            };

            // Send to MediatR pipeline
            var command = new ProcessMessageCommand
            {
                DocketName = docketName,
                Message = message
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Successfully processed gRPC message for docket {DocketName}. Forwarded to {Count} destinations.",
                    docketName,
                    result.DestinationsForwarded);

                // Return empty response (or could return transformed data)
                return Array.Empty<byte>();
            }
            else
            {
                _logger.LogError(
                    "Failed to process gRPC message for docket {DocketName}: {Error}",
                    docketName,
                    result.ErrorMessage);
                throw new RpcException(new Status(StatusCode.Internal, $"Processing failed: {result.ErrorMessage}"));
            }
        }
        catch (RpcException)
        {
            throw; // Re-throw gRPC exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling gRPC message for docket: {DocketName}", docketName);
            throw new RpcException(new Status(StatusCode.Internal, $"Internal error: {ex.Message}"));
        }
    }
}
