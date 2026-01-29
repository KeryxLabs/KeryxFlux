namespace KeryxFlux.Domain.Abstractions
{
    public interface IDocketMonitor
    {
        event EventHandler<MonitorErrorEventArgs> OnError;
        event EventHandler<MonitorInfoEventArgs> OnLoaded;
        event EventHandler<MonitorInfoEventArgs> OnUnloaded;
        event EventHandler<MonitorInfoEventArgs> OnReloaded;
        delegate void OnLoadedEventHandler(object sender, MonitorInfoEventArgs e);
        delegate void OnUnloadedEventHandler(object sender, MonitorInfoEventArgs e);
        delegate void OnReloadedEventHandler(object sender, MonitorInfoEventArgs e);
        delegate void OnErrorEventHandler(object sender, MonitorErrorEventArgs e);

        void Start();
    }
}
