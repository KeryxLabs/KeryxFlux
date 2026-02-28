using KeryxFlux.Contracts;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Ports;
using KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;
using KeryxFlux.Infrastructure.Receivers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Infrastructure.Factories;

/// <summary>
/// Factory for creating IReceiver instances based on configuration type.
/// Supports HTTP, RabbitMQ, and future receiver types (Kafka, TCP, etc.).
/// </summary>
public class ReceiverFactory : IReceiverFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IRabbitMqConnectionService _rabbitMqConnectionService;

    public ReceiverFactory(
        ILoggerFactory loggerFactory,
        IHttpContextAccessor httpContextAccessor,
        IRabbitMqConnectionService rabbitMqConnectionService)
    {
        _loggerFactory = loggerFactory;
        _httpContextAccessor = httpContextAccessor;
        _rabbitMqConnectionService = rabbitMqConnectionService;
    }

    public IReceiver Create(string type)
    {
        var receiverType = type.ToLowerInvariant();

        _loggerFactory.CreateLogger<ReceiverFactory>().LogInformation("Creating receiver of type: {Type}", receiverType);

        return receiverType switch
        {
            "http" => new HttpReceiver(
                _loggerFactory.CreateLogger<HttpReceiver>()),
            
            "rabbitmq" => throw new InvalidOperationException(
                "RabbitMQ receiver requires configuration. Use CreateWithConfig method."),
            
            _ => throw new NotSupportedException(
                $"Receiver type '{type}' is not supported. Supported types: http, rabbitmq")
        };
    }

    /// <summary>
    /// Create a receiver with full configuration (for RabbitMQ and other advanced receivers)
    /// </summary>
    public IReceiver CreateWithConfig(
        ReceiverConfiguration config,
        IReceiverPlugin plugin,
        Docket docket)
    {
        var receiverType = config.Type.ToLowerInvariant();

        _loggerFactory.CreateLogger<ReceiverFactory>().LogInformation("Creating receiver of type: {Type} with configuration", receiverType);

        return receiverType switch
        {
            "http" => new HttpReceiver(
                _loggerFactory.CreateLogger<HttpReceiver>()),
            
            "rabbitmq" => new RabbitMqReceiver(
                _rabbitMqConnectionService,
                _loggerFactory.CreateLogger<RabbitMqReceiver>(),
                config,
                plugin,
                docket),
            
            _ => throw new NotSupportedException(
                $"Receiver type '{config.Type}' is not supported. Supported types: http, rabbitmq")
        };
    }

    public bool Supports(string type)
    {
        var receiverType = type.ToLowerInvariant();
        return receiverType is "http" or "rabbitmq";
    }
}
