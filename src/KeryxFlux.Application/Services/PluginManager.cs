using Microsoft.Extensions.Logging;
using KeryxFlux.Contracts;
using KeryxFlux.Domain;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Application.FileSystem;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Application.Services;

public sealed class PluginManager : IPluginManager
{
    private readonly ILogger<PluginManager> _logger;
    private readonly ConcurrentDictionary<LibraryPath, LibraryMetadata> _store = [];

    // Track how many dockets are using each plugin
    private readonly ConcurrentDictionary<LibraryPath, int> _referenceCount = [];

    public PluginManager(ILogger<PluginManager> logger)
    {
        _logger = logger;
    }

    public static PluginManager Create() =>
        new(LoggerFactory.Create(cfg => cfg.SetMinimumLevel(LogLevel.Information)).CreateLogger<PluginManager>());

    // New interface methods
    public Result<IKeryxFluxPlugin> LoadPlugin(string pluginPath)
    {
        try
        {
            if (!File.Exists(pluginPath))
            {
                return Result.Failure<IKeryxFluxPlugin>(
                    new LoadingError($"Plugin file not found: {pluginPath}"));
            }

            var libraryPath = new LibraryPath(pluginPath);

            // Check if already loaded
            if (_store.TryGetValue(libraryPath, out var cachedMetadata))
            {
                // Increment reference count
                _referenceCount.AddOrUpdate(libraryPath, 1, (key, count) => count + 1);

                // Try to create instance from cached metadata
                if (TryGetInstance(cachedMetadata, out var cachedPlugin) && cachedPlugin is IKeryxFluxPlugin plugin)
                {
                    _logger.LogDebug("Using cached plugin from {PluginPath} (references: {RefCount})",
                        pluginPath, _referenceCount[libraryPath]);
                    return Result.Success(plugin);
                }
            }

            // Load the plugin using existing loader infrastructure
            var loadResult = Loader.LoadFromPath(libraryPath);
            if (loadResult.IsFailure)
            {
                return Result.Failure<IKeryxFluxPlugin>(
                    new LoadingError($"Failed to load plugin: {loadResult.Error}"));
            }

            var metadata = loadResult.Value!;
            _store[libraryPath] = metadata;

            // Initialize reference count to 1
            _referenceCount[libraryPath] = 1;

            // Create instance
            if (!TryGetInstance(metadata, out var instance) || instance is not IKeryxFluxPlugin keryxPlugin)
            {
                return Result.Failure<IKeryxFluxPlugin>(
                    new LoadingError("Plugin does not implement IKeryxFluxPlugin"));
            }

            _logger.LogInformation("Successfully loaded plugin {PluginName} v{Version} from {Path}",
                keryxPlugin.Name, keryxPlugin.Version, pluginPath);

            return Result.Success(keryxPlugin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading plugin from {PluginPath}", pluginPath);
            return Result.Failure<IKeryxFluxPlugin>(new LoadingError(ex.Message));
        }
    }

    public bool TryGetLoadedPlugin(string pluginPath, [NotNullWhen(true)] out IKeryxFluxPlugin? plugin)
    {
        plugin = null;
        var libraryPath = new LibraryPath(pluginPath);

        if (_store.TryGetValue(libraryPath, out var metadata))
        {
            if (TryGetInstance(metadata, out var instance) && instance is IKeryxFluxPlugin keryxPlugin)
            {
                plugin = keryxPlugin;
                return true;
            }
        }

        return false;
    }

    public void UnloadPlugin(string pluginPath)
    {
        var libraryPath = new LibraryPath(pluginPath);

        if (!_referenceCount.TryGetValue(libraryPath, out var count))
        {
            // Plugin not loaded or already unloaded
            _logger.LogWarning("Attempted to unload plugin that is not loaded: {PluginPath}", pluginPath);
            return;
        }

        // Decrement reference count
        var newCount = _referenceCount.AddOrUpdate(libraryPath, 0, (key, current) => Math.Max(0, current - 1));

        if (newCount == 0)
        {
            // No more references, actually unload
            if (_store.TryRemove(libraryPath, out _) && _referenceCount.TryRemove(libraryPath, out _))
            {
                _logger.LogInformation("Unloaded plugin from {PluginPath} (no more references)", pluginPath);
            }
        }
        else
        {
            _logger.LogDebug("Decremented reference count for {PluginPath} (references: {RefCount})",
                pluginPath, newCount);
        }
    }

    public bool TryGetInstance(LibraryMetadata metadata, [NotNullWhen(true)] out IKeryxFluxPlugin? plugin)
    {
        plugin = null;
        if (!_store.TryGetValue(metadata.Info.LibraryPath, out var libTypeInfo))
        {
            return false;
        }

        try
        {
            if (Activator.CreateInstance(libTypeInfo.Type) is IKeryxFluxPlugin newPlugin)
            {
                plugin = newPlugin;
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create plugin instance");
            return false;
        }
    }
}