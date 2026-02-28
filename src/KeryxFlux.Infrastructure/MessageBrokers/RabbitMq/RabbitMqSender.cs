using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;

/// <summary>
/// RabbitMQ sender that forwards messages to RabbitMQ exchanges using MassTransit.
/// </summary>
public class RabbitMqSender : ISender
{
    public string Type => "rabbitmq";

    private readonly IRabbitMqConnectionService _connectionService;
    private readonly ILogger<RabbitMqSender> _logger;

    public RabbitMqSender(
        IRabbitMqConnectionService connectionService,
        ILogger<RabbitMqSender> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            // For RabbitMQ, the destination name contains the connection string
            // and additional configuration should be in headers
            var connectionString = GetHeaderValue(message, "connection_string") 
                                ?? GetHeaderValue(message, "ConnectionString");
            var exchangeName = GetHeaderValue(message, "exchange_name") 
                            ?? GetHeaderValue(message, "exchange");
            var routingKey = GetHeaderValue(message, "routing_key") 
                          ?? GetHeaderValue(message, "topic");

            if (string.IsNullOrEmpty(connectionString))
            {
                return SendResult.Failure("RabbitMQ connection_string is required in message headers");
            }

            _logger.LogInformation(
                "Sending message to RabbitMQ. Exchange: {Exchange}, RoutingKey: {RoutingKey}, Size: {Size} bytes",
                exchangeName ?? "(default)",
                routingKey ?? "(none)",
                message.Payload.Length);

            // Publish message using connection service
            await _connectionService.PublishAsync(
                connectionString,
                message.Payload,
                exchangeName,
                routingKey,
                cancellationToken);

            _logger.LogInformation(
                "Successfully sent message to RabbitMQ. Destination: {Destination}",
                message.DestinationName);

            return SendResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send message to RabbitMQ destination: {Destination}",
                message.DestinationName);

            return SendResult.Failure($"RabbitMQ send failed: {ex.Message}");
        }
    }

    private static string? GetHeaderValue(OutboundMessage message, string key)
    {
        return message.Headers.TryGetValue(key, out var value) ? value : null;
    }
}
