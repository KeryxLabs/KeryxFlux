namespace KeryxFlux.Contracts;

/// <summary>
/// Base interface for all KeryxFlux plugins.
/// Provides identification and metadata for the plugin.
/// Use specialized interfaces (IReceiverPlugin, IPollerPlugin) for actual implementations.
/// </summary>
public interface IKeryxFluxPlugin
{
    /// <summary>
    /// Unique name for this plugin (used for logging and identification)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Version of the plugin (semantic versioning recommended)
    /// </summary>
    string Version { get; }
}
