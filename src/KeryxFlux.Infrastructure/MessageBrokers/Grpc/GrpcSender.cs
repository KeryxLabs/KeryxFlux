using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KeryxFlux.Infrastructure.MessageBrokers.Grpc;

/// <summary>
/// gRPC sender that forwards messages to gRPC services using Grpc.Net.Client.
/// </summary>
public class GrpcSender : ISender
{
    public string Type => "grpc";

    private readonly ILogger<GrpcSender> _logger;
    
    // Connection pooling: endpoint ? channel
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();

    public GrpcSender(ILogger<GrpcSender> logger)
    {
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = GetHeaderValue(message, "grpc_endpoint");
            var serviceName = GetHeaderValue(message, "service_name");
            var methodName = GetHeaderValue(message, "method_name") ?? "Process";
            var useTls = GetHeaderValue(message, "use_tls")?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? true;

            if (string.IsNullOrEmpty(endpoint))
            {
                return SendResult.Failure("gRPC grpc_endpoint is required in message headers");
            }

            if (string.IsNullOrEmpty(serviceName))
            {
                return SendResult.Failure("gRPC service_name is required in message headers");
            }

            _logger.LogInformation(
                "Sending message to gRPC. Endpoint: {Endpoint}, Service: {Service}, Method: {Method}, Size: {Size} bytes",
                endpoint,
                serviceName,
                methodName,
                message.Payload.Length);

            // Get or create channel for this endpoint
            var channel = _channels.GetOrAdd(endpoint, ep =>
            {
                var address = useTls ? $"https://{ep}" : $"http://{ep}";
                return GrpcChannel.ForAddress(address, new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                        EnableMultipleHttp2Connections = true
                    }
                });
            });

            // Create method definition for unary call
            var method = new Method<ByteString, ByteString>(
                type: MethodType.Unary,
                serviceName: serviceName,
                name: methodName,
                requestMarshaller: Marshallers.Create(
                    serializer: bytes => bytes.ToByteArray(),
                    deserializer: bytes => ByteString.CopyFrom(bytes)),
                responseMarshaller: Marshallers.Create(
                    serializer: bytes => bytes.ToByteArray(),
                    deserializer: bytes => ByteString.CopyFrom(bytes))
            );

            // Create call invoker
            var callInvoker = channel.CreateCallInvoker();

            // Add correlation ID header
            var headers = new Metadata
            {
                { "x-correlation-id", message.CorrelationId },
                { "content-type", message.ContentType }
            };

            // Make unary call
            var request = ByteString.CopyFrom(message.Payload);
            var response = await callInvoker.AsyncUnaryCall(
                method,
                null,
                new CallOptions(headers: headers, cancellationToken: cancellationToken),
                request);

            _logger.LogInformation(
                "Successfully sent message to gRPC. Endpoint: {Endpoint}, Service: {Service}",
                endpoint,
                serviceName);

            return SendResult.Success();
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex,
                "gRPC call error for destination: {Destination}. Status: {Status}, Detail: {Detail}",
                message.DestinationName,
                ex.StatusCode,
                ex.Status.Detail);

            return SendResult.Failure($"gRPC call failed: {ex.Status.Detail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send message to gRPC destination: {Destination}",
                message.DestinationName);

            return SendResult.Failure($"gRPC send failed: {ex.Message}");
        }
    }

    private static string? GetHeaderValue(OutboundMessage message, string key)
    {
        return message.Headers.TryGetValue(key, out var value) ? value : null;
    }
}
