using KeryxFlux.Domain.Ports;
using KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;
using KeryxFlux.Infrastructure.Senders;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Infrastructure.Factories;

/// <summary>
/// Factory for creating ISender instances based on configuration type.
/// Supports HTTP, RabbitMQ, and future sender types (Kafka, TCP, SFTP, etc.).
/// </summary>
public class SenderFactory : ISenderFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRabbitMqConnectionService _rabbitMqConnectionService;

    public SenderFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IRabbitMqConnectionService rabbitMqConnectionService)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _rabbitMqConnectionService = rabbitMqConnectionService;
    }

    public ISender Create(string type)
    {
        var senderType = type.ToLowerInvariant();

        _loggerFactory.CreateLogger<SenderFactory>().LogInformation("Creating sender of type: {Type}", senderType);

        return senderType switch
        {
            "http" => new HttpSender(
                _httpClientFactory,
                _loggerFactory.CreateLogger<HttpSender>()),
            
            "rabbitmq" => new RabbitMqSender(
                _rabbitMqConnectionService,
                _loggerFactory.CreateLogger<RabbitMqSender>()),
            
            _ => throw new NotSupportedException(
                $"Sender type '{type}' is not supported. Supported types: http, rabbitmq")
        };
    }

    public bool Supports(string type)
    {
        var senderType = type.ToLowerInvariant();
        return senderType is "http" or "rabbitmq";
    }
}
