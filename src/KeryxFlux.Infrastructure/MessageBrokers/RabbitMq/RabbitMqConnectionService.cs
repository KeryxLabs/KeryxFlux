using MassTransit;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;

/// <summary>
/// Manages RabbitMQ connections using MassTransit.
/// Provides connection pooling per connection string.
/// </summary>
public class RabbitMqConnectionService : IRabbitMqConnectionService
{
    private readonly ILogger<RabbitMqConnectionService> _logger;
    private readonly ConcurrentDictionary<string, IBusControl> _busInstances = new();

    public RabbitMqConnectionService(ILogger<RabbitMqConnectionService> logger)
    {
        _logger = logger;
    }

    public Task<IBusControl> GetOrCreateBusAsync(
        string connectionString,
        Action<IRabbitMqBusFactoryConfigurator>? configure = null)
    {
        var bus = _busInstances.GetOrAdd(connectionString, key =>
        {
            _logger.LogInformation("Creating new RabbitMQ bus connection for: {ConnectionString}", 
                MaskConnectionString(key));

            var busControl = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                // Parse connection string (amqp://user:pass@host:port/vhost)
                var uri = new Uri(key);
                cfg.Host(uri);

                // Apply additional configuration if provided
                configure?.Invoke(cfg);
            });

            // Start the bus
            busControl.Start();
            
            _logger.LogInformation("RabbitMQ bus started successfully");

            return busControl;
        });

        return Task.FromResult(bus);
    }

    public async Task PublishAsync<T>(
        string connectionString,
        T message,
        string? exchangeName = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var bus = await GetOrCreateBusAsync(connectionString);

        if (exchangeName != null && routingKey != null)
        {
            // Publish to specific exchange with routing key
            var sendEndpoint = await bus.GetSendEndpoint(new Uri($"exchange:{exchangeName}?bind=true&routingKey={routingKey}"));
            await sendEndpoint.Send(message, cancellationToken);
        }
        else
        {
            // Standard publish (topic exchange)
            await bus.Publish(message, cancellationToken);
        }

        _logger.LogInformation(
            "Published message to RabbitMQ. Exchange: {Exchange}, RoutingKey: {RoutingKey}",
            exchangeName ?? "(default)",
            routingKey ?? "(none)");
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping all RabbitMQ connections...");

        var stopTasks = _busInstances.Values.Select(bus => bus.StopAsync(cancellationToken));
        await Task.WhenAll(stopTasks);

        _busInstances.Clear();

        _logger.LogInformation("All RabbitMQ connections stopped");
    }

    private static string MaskConnectionString(string connectionString)
    {
        try
        {
            var uri = new Uri(connectionString);
            return $"amqp://***:***@{uri.Host}:{uri.Port}{uri.AbsolutePath}";
        }
        catch
        {
            return "***";
        }
    }
}
