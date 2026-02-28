using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Ports;

/// <summary>
/// Service interface for managing TCP listeners.
/// Implementations handle dynamic registration/unregistration of TCP sockets per docket.
/// </summary>
public interface ITcpReceiverService
{
    /// <summary>
    /// Register a TCP listener for the specified docket.
    /// Opens TCP socket on configured port, starts listening for connections.
    /// </summary>
    Task RegisterReceiverForDocketAsync(Docket docket);
    
    /// <summary>
    /// Unregister a TCP listener for the specified docket.
    /// Closes TCP socket, stops accepting new connections.
    /// </summary>
    Task UnregisterReceiverForDocketAsync(string docketName);
}
