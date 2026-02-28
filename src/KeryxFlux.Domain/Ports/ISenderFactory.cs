namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Factory for creating ISender instances based on configuration.
/// Supports HTTP, RabbitMQ, Kafka, and other sender types.
/// </summary>
public interface ISenderFactory
{
    /// <summary>
    /// Create a sender instance based on the type specified in configuration
    /// </summary>
    /// <param name="type">Sender type (http, rabbitmq, kafka, etc.)</param>
    /// <returns>Configured sender instance</returns>
    ISender Create(string type);
    
    /// <summary>
    /// Check if the factory supports a given sender type
    /// </summary>
    /// <param name="type">Sender type to check</param>
    /// <returns>True if supported, false otherwise</returns>
    bool Supports(string type);
}
