using Microsoft.Extensions.Logging;
using KeryxFlux.Domain;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Application.FileSystem;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Application.Services;

public class DocketManager : IDocketManager
{
    private readonly ILogger<DocketManager> _logger;
    private readonly IPluginManager _pluginManager;
    private readonly ConcurrentDictionary<DocketInfo, Docket> _store = [];

    public DocketManager(ILogger<DocketManager> logger, IPluginManager pluginManager)
    {
        _logger = logger;
        _pluginManager = pluginManager;
    }

    public static DocketManager Create() =>
            new(LoggerFactory.Create(cfg =>
                    cfg.SetMinimumLevel(LogLevel.Information))
                    .CreateLogger<DocketManager>(),
                PluginManager.Create());

    public IEnumerable<Docket> GetLoadedDockets() => _store.Values.AsEnumerable();

    public IEnumerable<Docket> GetReceiverDockets() =>
        _store.Values.Where(d => d.Type == DocketType.Receiver);

    public IEnumerable<Docket> GetPollerDockets() =>
        _store.Values.Where(d => d.Type == DocketType.Poller);

    public Docket? GetDocketByName(string docketName) =>
        _store.Where(kp => docketName.Equals(kp.Key.DocketName)).Select(kp => kp.Value).FirstOrDefault();

    public bool TryGetDocket(string docketName, [NotNullWhen(true)] out Docket? docket)
    {
        docket = null;
        if (string.IsNullOrWhiteSpace(docketName)) return false;
        try
        {
            docket = GetDocketByName(docketName);
            return docket is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool TryLoad(DocketPath docketPath, [NotNullWhen(true)] out Docket? docket)
    {
        docket = null;
        try
        {
            _logger.LogDebug("Loading docket from {DocketPath}", docketPath);
            
            var loadRes = Loader.LoadDocket(docketPath);

            if (loadRes.IsFailure)
            {
                _logger.LogError("Failed to load docket from {DocketPath}: {Error}", docketPath, loadRes.Error);
                return false;
            }

            Docket loadedDocket = loadRes.Value!;
            
            _logger.LogDebug("Attempting to load plugin from: {PluginLocation}", loadedDocket.PluginLocation);
            
            // Load the plugin using new API (path as-is from YAML)
            var pluginResult = _pluginManager.LoadPlugin(loadedDocket.PluginLocation);
            if (pluginResult.IsFailure)
            {
                _logger.LogError("Failed to load plugin for docket {DocketName} from {PluginLocation}: {Error}",
                    loadedDocket.Name, loadedDocket.PluginLocation, pluginResult.Error);
                return false;
            }

            _logger.LogInformation("Successfully loaded plugin {PluginName} for docket {DocketName}",
                pluginResult.Value!.Name, loadedDocket.Name);

            // Store docket
            var docketInfo = new DocketInfo(loadedDocket.Name, docketPath);
            _store.TryAdd(docketInfo, loadedDocket);

            docket = loadedDocket;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception loading docket from {DocketPath}", docketPath);
            return false;
        }
    }

    public bool TryUnload(DocketPath docketPath, [NotNullWhen(true)] out Docket? docket)
    {
        docket = null;
        try
        {
            _logger.LogDebug("Unloading docket from {DocketPath}", docketPath);
            
            var currentInfo = GetDocketInfoByPath(docketPath);

            if (currentInfo.Equals(default))
            {
                _logger.LogWarning("Docket not found for path {DocketPath}", docketPath);
                return false;
            }

            if (!_store.TryRemove(currentInfo, out var loadedDocket))
            {
                _logger.LogError("Failed to remove docket {DocketName} from store", currentInfo.DocketName);
                return false;
            }

            // Unload plugin using new API
            _pluginManager.UnloadPlugin(loadedDocket.PluginLocation);
            
            _logger.LogInformation("Successfully unloaded docket {DocketName}", loadedDocket.Name);

            docket = loadedDocket;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception unloading docket from {DocketPath}", docketPath);
            return false;
        }
    }

    private DocketInfo GetDocketInfoByPath(DocketPath docketPath) =>
        _store.Where(kp => docketPath.Equals(kp.Key.DocketPath)).Select(kp => kp.Key).FirstOrDefault();
}