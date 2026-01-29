using Microsoft.Extensions.Logging;
using KeryxFlux.Domain;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using System.Runtime.InteropServices;

namespace KeryxFlux.Application.FileSystem
{
    public class DocketMonitor : IDocketMonitor
    {
        private readonly ILogger<DocketMonitor> _logger;
        private readonly IDocketManager _manager;
        private readonly string _rootDir;
        
        // Watch for all YAML files - convention: any YAML in dockets/ is a docket
        private const string _ymlFilter = "*.yml";
        private const string _yamlFilter = "*.yaml";
        
        private readonly FileSystemWatcher _ymlWatcher;
        private readonly FileSystemWatcher _yamlWatcher;

        public event EventHandler<MonitorErrorEventArgs>? OnError;
        public event EventHandler<MonitorInfoEventArgs>? OnLoaded;
        public event EventHandler<MonitorInfoEventArgs>? OnUnloaded;
        public event EventHandler<MonitorInfoEventArgs>? OnReloaded;

        public DocketMonitor(ILogger<DocketMonitor> logger, IDocketManager manager, string rootDir)
        {
            _logger = logger;
            _manager = manager;
            _rootDir = rootDir;
            
            // Ensure dockets directory exists
            if (!Directory.Exists(_rootDir))
            {
                Directory.CreateDirectory(_rootDir);
                _logger.LogInformation("Created dockets directory at {RootDir}", _rootDir);
            }

            // DocketMonitor creates and owns its FileSystemWatchers
            _ymlWatcher = new FileSystemWatcher(_rootDir, _ymlFilter);
            _yamlWatcher = new FileSystemWatcher(_rootDir, _yamlFilter);
        }

        public void Start()
        {
            _logger.LogInformation("Starting DocketMonitor for directory: {RootDir}", _rootDir);
            
            #region yaml Watcher
            _yamlWatcher.NotifyFilter = NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size;

            _yamlWatcher.Changed += FileSystemWatcher_ChangedAsync;
            _yamlWatcher.Created += FileSystemWatcher_CreatedAsync;
            _yamlWatcher.Renamed += FileSystemWatcher_RenamedAsync;
            _yamlWatcher.Deleted += FileSystemWatcher_DeletedAsync;
            _yamlWatcher.EnableRaisingEvents = true;
            _yamlWatcher.IncludeSubdirectories = true;
            #endregion

            #region yml Watcher
            _ymlWatcher.NotifyFilter = NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size;

            _ymlWatcher.Changed += FileSystemWatcher_ChangedAsync;
            _ymlWatcher.Created += FileSystemWatcher_CreatedAsync;
            _ymlWatcher.Renamed += FileSystemWatcher_RenamedAsync;
            _ymlWatcher.Deleted += FileSystemWatcher_DeletedAsync;
            _ymlWatcher.EnableRaisingEvents = true;
            _ymlWatcher.IncludeSubdirectories = true;
            #endregion

            StartMonitoring();
        }

        protected virtual void OnDocketLoaded(MonitorInfoEventArgs e)
        {
            OnLoaded?.Invoke(this, e);
        }
        protected virtual void OnDocketUnloaded(MonitorInfoEventArgs e)
        {
            OnUnloaded?.Invoke(this, e);
        }
        protected virtual void OnDocketReloaded(MonitorInfoEventArgs e)
        {
            OnReloaded?.Invoke(this, e);
        }


        private void StartMonitoring()
        {
            _logger.LogInformation("Scanning for existing docket files...");
            
            // Load all .yaml files
            var yamlFiles = Directory.GetFiles(_rootDir, _yamlFilter, SearchOption.AllDirectories).ToList();
            
            // Load all .yml files
            var ymlFiles = Directory.GetFiles(_rootDir, _ymlFilter, SearchOption.AllDirectories);
            yamlFiles.AddRange(ymlFiles);

            _logger.LogInformation("Found {Count} docket files to load", yamlFiles.Count);

            foreach (var file in yamlFiles)
            {
                var result = LoadDocket(file);
                if (result.IsFailure)
                {
                    _logger.LogError("Failed to load docket from {File}: {Error}", file, result.Error);
                }
            }

            _logger.LogInformation("Initial docket loading complete. Monitoring for changes...");
        }

        private Result LoadDocket(string file)
        {
            try
            {
                var path = new DocketPath(file);

                if (!_manager.TryLoad(path, out var docket))
                {
                    return LoadingError.InvalidYmlFile;
                }

                OnLoaded?.Invoke(this, new MonitorInfoEventArgs(docket, new DocketInfo(docket.Name, path)));

                return Result.Success();
            }
            catch (Exception ex)
            {
                var error = new Error(LoadingError.InvalidYmlFile.Code, ex.Message);
                OnError?.Invoke(this, new MonitorErrorEventArgs(error));
                return error;
            }
        }

        private void FileSystemWatcher_RenamedAsync(object sender, RenamedEventArgs e)
        {
            WaitForFileLockRelease(e.FullPath);
            var newDocketPath = new DocketPath(e.FullPath);
            var oldDocketPath = new DocketPath(e.OldFullPath);
            if (!_manager.TryUnload(oldDocketPath, out _))
            {
                OnError?.Invoke(this, new MonitorErrorEventArgs(LoadingError.UnableToUnloadDocket(oldDocketPath)));
                return;
            }

            if (!_manager.TryLoad(newDocketPath, out var docket))
            {
                OnError?.Invoke(this, new MonitorErrorEventArgs(LoadingError.UnableToLoadDocket(newDocketPath)));
                return;
            }

            OnReloaded?.Invoke(this, new MonitorInfoEventArgs(docket, new(docket.Name, newDocketPath)));

        }
        private void FileSystemWatcher_ChangedAsync(object sender, FileSystemEventArgs e)
        {
            WaitForFileLockRelease(e.FullPath);
            var docketPath = new DocketPath(e.FullPath);
            if (!_manager.TryUnload(docketPath, out _))
            {
                OnError?.Invoke(this, new MonitorErrorEventArgs(LoadingError.UnableToUnloadDocket(docketPath)));
                return;
            }

            if (!_manager.TryLoad(docketPath, out var docket))
            {
                OnError?.Invoke(this, new MonitorErrorEventArgs(LoadingError.UnableToLoadDocket(docketPath)));
                return;
            }

            OnReloaded?.Invoke(this, new MonitorInfoEventArgs(docket, new(docket.Name, docketPath)));

        }
        private void FileSystemWatcher_DeletedAsync(object sender, FileSystemEventArgs e)
        {
            WaitForFileLockRelease(e.FullPath);
            var docketPath = new DocketPath(e.FullPath);

            if (!_manager.TryUnload(docketPath, out var docket))
            {
                OnError?.Invoke(this, new MonitorErrorEventArgs(LoadingError.UnableToUnloadDocket(docketPath)));
                return;
            }

            OnUnloaded?.Invoke(this, new MonitorInfoEventArgs(docket, new DocketInfo(docket.Name, docketPath)));

        }
        private void FileSystemWatcher_CreatedAsync(object sender, FileSystemEventArgs e)
        {
            WaitForFileLockRelease(e.FullPath);

            var docketPath = new DocketPath(e.FullPath);
            if (!_manager.TryLoad(docketPath, out var docket))
            {
                OnError?.Invoke(this, new MonitorErrorEventArgs(LoadingError.UnableToLoadDocket(docketPath)));
                return;
            }
            OnLoaded?.Invoke(this, new(docket, new DocketInfo(docket.Name, docketPath)));
        }


        private const int ERROR_SHARING_VIOLATION = 32;
        private const int ERROR_LOCK_VIOLATION = 33;
        private static bool IsFileLocked(string path)
        {
            if (File.Exists(path))
            {
                FileStream stream = null;

                try
                {
                    stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                }
                catch (Exception ex)
                {
                    int errorCode = Marshal.GetHRForException(ex) & (1 << 16) - 1;
                    if (ex is IOException && errorCode == ERROR_LOCK_VIOLATION || errorCode == ERROR_SHARING_VIOLATION)
                        return true;

                }
                finally
                {
                    stream?.Dispose();
                }


            }
            return false;
        }
        private static void WaitForFileLockRelease(string path)
        {
            while (IsFileLocked(path))
            {
                //Do nothing while file unlocks
            }
        }
    }
}

