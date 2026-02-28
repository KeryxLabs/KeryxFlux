using MassTransit;

namespace KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;

/// <summary>
/// Manages RabbitMQ connections and bus instances using MassTransit.
/// Provides connection pooling and lifecycle management.
/// </summary>
public interface IRabbitMqConnectionService
{
    /// <summary>
    /// Get or create a RabbitMQ bus control instance for the given connection string.
    /// Connections are pooled and reused.
    /// </summary>
    /// <param name="connectionString">RabbitMQ connection string (amqp://...)</param>
    /// <param name="configure">Optional configuration action for the bus</param>
    /// <returns>Configured IBusControl instance</returns>
    Task<IBusControl> GetOrCreateBusAsync(
        string connectionString, 
        Action<IRabbitMqBusFactoryConfigurator>? configure = null);
    
    /// <summary>
    /// Publish a message to RabbitMQ exchange
    /// </summary>
    Task PublishAsync<T>(
        string connectionString,
        T message,
        string? exchangeName = null,
        string? routingKey = null,
        CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Stop and dispose all connections
    /// </summary>
    Task StopAllAsync(CancellationToken cancellationToken = default);
}
