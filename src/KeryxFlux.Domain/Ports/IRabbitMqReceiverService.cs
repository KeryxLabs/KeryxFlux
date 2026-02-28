using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Service interface for managing RabbitMQ consumers.
/// Implementations handle dynamic registration/unregistration of consumers per docket.
/// </summary>
public interface IRabbitMqReceiverService
{
    /// <summary>
    /// Register a RabbitMQ consumer for the specified docket.
    /// </summary>
    Task RegisterConsumerForDocketAsync(Docket docket);
    
    /// <summary>
    /// Unregister a RabbitMQ consumer for the specified docket.
    /// </summary>
    Task UnregisterConsumerForDocketAsync(string docketName);
}
