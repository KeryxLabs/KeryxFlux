using Microsoft.Extensions.Logging;
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
        private const string _ymlFilter = "*rqstr-docket.yml";
        private const string _yAmlFilter = "*rqstr-docket.yaml";
        private readonly FileSystemWatcher _ymlWatcher;
        private readonly FileSystemWatcher _yAmlWatcher;

        public event EventHandler<MonitorErrorEventArgs>? OnError;
        public event EventHandler<MonitorInfoEventArgs>? OnLoaded;
        public event EventHandler<MonitorInfoEventArgs>? OnUnloaded;
        public event EventHandler<MonitorInfoEventArgs>? OnReloaded;

        public DocketMonitor(ILogger<DocketMonitor> logger, IDocketManager manager, string rootDir, FileSystemWatcher ymlWatcher, FileSystemWatcher yAmlWatcher)
        {
            _logger = logger;
            _manager = manager;
            _rootDir = rootDir;
            if (!Directory.Exists(_rootDir))
                Directory.CreateDirectory(_rootDir);

            _ymlWatcher = ymlWatcher;
            _yAmlWatcher = yAmlWatcher;
        }

        public void Start()
        {
            #region yAml Watcher
            _yAmlWatcher.NotifyFilter = NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.Security;

            _yAmlWatcher.Changed += FileSystemWatcher_ChangedAsync;
            _yAmlWatcher.Created += FileSystemWatcher_CreatedAsync;
            _yAmlWatcher.Renamed += FileSystemWatcher_RenamedAsync;
            _yAmlWatcher.Deleted += FileSystemWatcher_DeletedAsync;
            _yAmlWatcher.EnableRaisingEvents = true;
            _yAmlWatcher.IncludeSubdirectories = true;
            #endregion

            #region yml Watcher
            _ymlWatcher.NotifyFilter = NotifyFilters.CreationTime
                | NotifyFilters.DirectoryName
                | NotifyFilters.FileName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.Security;

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
            var files = Directory.GetFiles(_rootDir, _yAmlFilter, SearchOption.AllDirectories).ToList();
            files.AddRange(Directory.GetFiles(_rootDir, _ymlFilter, SearchOption.AllDirectories));

            foreach (var item in files)
            {
                var res = LoadDocket(item);
                if (res.IsFailure)
                {

                }
            }

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
