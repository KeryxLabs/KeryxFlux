namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Factory for creating IReceiver instances based on configuration.
/// Supports HTTP, RabbitMQ, Kafka, and other receiver types.
/// </summary>
public interface IReceiverFactory
{
    /// <summary>
    /// Create a receiver instance based on the type specified in configuration
    /// </summary>
    /// <param name="type">Receiver type (http, rabbitmq, kafka, etc.)</param>
    /// <returns>Configured receiver instance</returns>
    IReceiver Create(string type);
    
    /// <summary>
    /// Check if the factory supports a given receiver type
    /// </summary>
    /// <param name="type">Receiver type to check</param>
    /// <returns>True if supported, false otherwise</returns>
    bool Supports(string type);
}
