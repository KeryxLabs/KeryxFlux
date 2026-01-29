using Microsoft.Extensions.Logging;
using KeryxFlux.Domain;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Application.FileSystem;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace KeryxFlux.Application.Services
{
    internal sealed class PluginManager : IPluginManager
    {
        private readonly ILogger<PluginManager> _logger;
        private readonly ConcurrentDictionary<LibraryPath, LibraryMetadata> _store = [];

        public PluginManager(ILogger<PluginManager> logger)
        {
            _logger = logger;
        }
        public static PluginManager Create() =>
            new(LoggerFactory.Create(cfg => cfg.SetMinimumLevel(LogLevel.Information)).CreateLogger<PluginManager>());


        public bool TryGetInstance(LibraryMetadata metadata, [NotNullWhen(true)] out IReqStrAdapter? plugin)
        {
            plugin = null;
            if (!_store.TryGetValue(metadata.Info.LibraryPath, out var libTypeInfo))
            {
                return false;
            }

            try
            {
                if (Activator.CreateInstance(libTypeInfo.Type) is IReqStrAdapter newPlugin)
                {
                    plugin = newPlugin;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

        }


        public bool TryGetMetadata(LibraryPath libraryPath, [NotNullWhen(true)] out LibraryMetadata? metadata)
        {
            if (_store.TryGetValue(libraryPath, out var storedMeta))
            {
                metadata = storedMeta;
                return true;
            }
            else
            {
                metadata = null;
                return false;
            }
        }

        public bool TryLoad(LibraryPath libraryPath, [NotNullWhen(true)] out LibraryMetadata? metadata)
        {

            metadata = null;
            if (_store.TryGetValue(libraryPath, out var storedMeta))
            {
                if (!Loader.VersionChanged(libraryPath, storedMeta.Info.LibraryVersion ?? new()))
                {
                    metadata = storedMeta;
                    return true;
                }

                if (!_store.TryRemove(libraryPath, out var _))
                {
                    return false;
                }
            }

            var loadResults = Loader.LoadFromPath(libraryPath);

            if (loadResults.IsFailure)
            {

                return false;
            }

            var loadedMeta = loadResults.Value!;

            _store.AddOrUpdate(libraryPath, loadedMeta, (k, v) => v = loadedMeta);
            metadata = loadedMeta;
            return true;
        }

        public bool TryUnload(LibraryMetadata info)
        {
            if (!_store.TryRemove(info.Info.LibraryPath, out var _))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }
}

