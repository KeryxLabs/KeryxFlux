using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Service interface for managing gRPC receivers.
/// Implementations handle dynamic registration/unregistration of gRPC services per docket.
/// </summary>
public interface IGrpcReceiverService
{
    /// <summary>
    /// Register a gRPC receiver for the specified docket.
    /// </summary>
    Task RegisterReceiverForDocketAsync(Docket docket);
    
    /// <summary>
    /// Unregister a gRPC receiver for the specified docket.
    /// </summary>
    Task UnregisterReceiverForDocketAsync(string docketName);
}
