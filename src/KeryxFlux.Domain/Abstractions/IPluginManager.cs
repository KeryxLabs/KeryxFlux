using KeryxFlux.Contracts;
using KeryxFlux.Domain.Models;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Domain.Abstractions;

/// <summary>
/// Manages loading and lifecycle of transformation plugins
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Load a plugin from the specified file path
    /// </summary>
    /// <param name="pluginPath">Path to the plugin DLL</param>
    /// <returns>Result containing the loaded plugin or error information</returns>
    Result<IKeryxFluxPlugin> LoadPlugin(string pluginPath);

    /// <summary>
    /// Get a cached plugin instance if already loaded
    /// </summary>
    bool TryGetLoadedPlugin(string pluginPath, [NotNullWhen(true)] out IKeryxFluxPlugin? plugin);

    /// <summary>
    /// Unload a plugin and free its resources
    /// </summary>
    void UnloadPlugin(string pluginPath);

    // Legacy methods for compatibility during migration
    [Obsolete("Use LoadPlugin instead")]
    bool TryGetInstance(LibraryMetadata metadata, [NotNullWhen(true)] out IKeryxFluxPlugin? plugin);

    [Obsolete("Use LoadPlugin instead")]
    bool TryGetMetadata(LibraryPath libraryPath, [NotNullWhen(true)] out LibraryMetadata? metadata);

    [Obsolete("Use LoadPlugin instead")]
    bool TryLoad(LibraryPath libraryPath, [NotNullWhen(true)] out LibraryMetadata? metadata);

    [Obsolete("Use UnloadPlugin instead")]
    bool TryUnload(LibraryMetadata metadata);
}
