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

    /// <summary>
    /// Internal method for creating plugin instances from metadata
    /// </summary>
    bool TryGetInstance(LibraryMetadata metadata, [NotNullWhen(true)] out IKeryxFluxPlugin? plugin);
}

