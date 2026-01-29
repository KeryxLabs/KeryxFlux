using Microsoft.Extensions.Logging;
using KeryxFlux.Domain;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Application.FileSystem;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Application.Services
{
    internal class DocketManager : IDocketManager
    {
        private readonly ILogger<DocketManager> _logger;
        private readonly IPluginManager _pluginManager;
        private readonly ConcurrentDictionary<DocketInfo, Docket> _store = [];
        private readonly ConcurrentDictionary<LibraryMetadata, int> _pluginToDocketReferences = [];

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

        public bool TryGetPluginInstance(string docketName, [NotNullWhen(true)] out IReqStrAdapter? plugin) 
        {
            plugin = default;
            try
            {
                var docket = GetDocketByName(docketName);
                plugin = GetDocketPluginInstance(GetDocketMetadata(docket)) ?? default;
                return plugin is not null;

            }
            catch (Exception)
            {
                return false;
            }


        }

        public bool TryGetPluginType(string docketName, [NotNullWhen(true)] out Type? type)
        {
            type = null;
            try
            {
                var docket = GetDocketByName(docketName);
                type = GetDocketMetadata(docket)?.Type;

                return type is not null;

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
                var loadRes = Loader.LoadDocket(docketPath);

                if (loadRes.IsFailure)
                {
                    return false;
                }

                Docket loadedDocket = loadRes.Value!;
                var docketInfo = new DocketInfo(loadedDocket.Name, docketPath);
                _store.TryAdd(docketInfo, loadedDocket);

                var libraryPath = new LibraryPath(loadedDocket.PluginLocation);

                if (!_pluginManager.TryLoad(libraryPath, out var metadata))
                {
                    return false;
                }

                _pluginToDocketReferences.AddOrUpdate(metadata, 1, (meta, val) => val + 1);
                docket = loadedDocket;
                return docket is not null;

            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TryUnload(DocketPath docketPath, [NotNullWhen(true)] out Docket? docket)
        {
            docket = null;
            try
            {

                var currentInfo = GetDocketInfoByPath(docketPath);

                if (currentInfo.Equals(default))
                {
                    return false;
                }

                if (!_store.TryRemove(currentInfo, out var loadedDocket))
                {
                    return false;
                }

                if (!_pluginManager.TryGetMetadata(new LibraryPath(loadedDocket.PluginLocation), out var meta))
                {
                    return false;
                }

                if (_pluginToDocketReferences.TryGetValue(meta, out var count))
                {
                    _pluginToDocketReferences[meta] = count - 1;
                    if (_pluginToDocketReferences[meta] == 0)
                        _pluginToDocketReferences.TryRemove(meta, out _);
                }

                docket = loadedDocket;
                return docket is not null;

            }
            catch (Exception)
            {
                return false;
            }


        }


        private Docket? GetDocketByName(string docketName) =>
            _store.Where(kp => docketName.Equals(kp.Key.DocketName)).Select(kp => kp.Value).FirstOrDefault();
        private DocketInfo GetDocketInfoByPath(DocketPath docketPath) =>
            _store.Where(kp => docketPath.Equals(kp.Key.DocketPath)).Select(kp => kp.Key).FirstOrDefault();
        private LibraryMetadata? GetDocketMetadata(Docket? docket) => docket is not null &&
            _pluginManager.TryGetMetadata(new LibraryPath(docket.PluginLocation), out var pluginMetadata) ?
                pluginMetadata
                : null;
        private IReqStrAdapter? GetDocketPluginInstance(LibraryMetadata? libraryMetadata) => libraryMetadata is not null &&
            _pluginManager.TryGetInstance(libraryMetadata, out var plugin) ?
                plugin
                : null;
    }
}

