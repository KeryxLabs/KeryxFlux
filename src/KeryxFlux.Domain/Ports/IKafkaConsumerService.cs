using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Service interface for managing Kafka consumers.
/// Implementations handle dynamic registration/unregistration of consumers per docket.
/// </summary>
public interface IKafkaConsumerService
{
    /// <summary>
    /// Register a Kafka consumer for the specified docket.
    /// </summary>
    Task RegisterConsumerForDocketAsync(Docket docket);
    
    /// <summary>
    /// Unregister a Kafka consumer for the specified docket.
    /// </summary>
    Task UnregisterConsumerForDocketAsync(string docketName);
}
