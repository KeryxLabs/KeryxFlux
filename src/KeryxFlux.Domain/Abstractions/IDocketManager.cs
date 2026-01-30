using KeryxFlux.Domain.Models;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Domain.Abstractions;

/// <summary>
/// Manages loading, unloading, and accessing docket configurations
/// </summary>
public interface IDocketManager
{
    /// <summary>
    /// Load a docket from a file path
    /// </summary>
    bool TryLoad(DocketPath docketPath, [NotNullWhen(true)] out Docket? docket);

    /// <summary>
    /// Unload a docket by its file path
    /// </summary>
    bool TryUnload(DocketPath docketPath, [NotNullWhen(true)] out Docket? docket);

    /// <summary>
    /// Get a docket by its name
    /// </summary>
    bool TryGetDocket(string docketName, [NotNullWhen(true)] out Docket? docket);

    /// <summary>
    /// Get docket by name (simplified method for MediatR handlers)
    /// </summary>
    Docket? GetDocketByName(string docketName);

    /// <summary>
    /// Get all currently loaded dockets
    /// </summary>
    IEnumerable<Docket> GetLoadedDockets();

    /// <summary>
    /// Get all receiver-type dockets
    /// </summary>
    IEnumerable<Docket> GetReceiverDockets();

    /// <summary>
    /// Get all poller-type dockets
    /// </summary>
    IEnumerable<Docket> GetPollerDockets();
}

