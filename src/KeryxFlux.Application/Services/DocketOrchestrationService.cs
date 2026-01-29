using KeryxFlux.Application.FileSystem;
using KeryxFlux.Domain.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Application.Services;

/// <summary>
/// Orchestrates docket loading and monitoring as a hosted service.
/// Handles the coordination between DocketMonitor and DocketManager.
/// Pure orchestration - no web framework coupling.
/// </summary>
public class DocketOrchestrationService : IHostedService
{
    private readonly ILogger<DocketOrchestrationService> _logger;
    private readonly IDocketMonitor _docketMonitor;

    public DocketOrchestrationService(
        ILogger<DocketOrchestrationService> logger,
        IDocketMonitor docketMonitor)
    {
        _logger = logger;
        _docketMonitor = docketMonitor;
    }

    /// <summary>
    /// Called when the host is starting.
    /// Subscribes to docket events and starts monitoring.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DocketOrchestrationService");

        // Subscribe to docket loading events
        _docketMonitor.OnLoaded += (sender, e) => HandleDocketLoaded(e);
        _docketMonitor.OnUnloaded += (sender, e) => HandleDocketUnloaded(e);
        _docketMonitor.OnReloaded += (sender, e) => HandleDocketReloaded(e);
        _docketMonitor.OnError += (sender, e) => HandleDocketError(e);

        // Start monitoring for dockets
        _docketMonitor.Start();

        _logger.LogInformation("DocketOrchestrationService started");
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when the host is stopping.
    /// Cleanup and unsubscribe from events.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping DocketOrchestrationService");
        
        // Unsubscribe from events
        _docketMonitor.OnLoaded -= (sender, e) => HandleDocketLoaded(e);
        _docketMonitor.OnUnloaded -= (sender, e) => HandleDocketUnloaded(e);
        _docketMonitor.OnReloaded -= (sender, e) => HandleDocketReloaded(e);
        _docketMonitor.OnError -= (sender, e) => HandleDocketError(e);
        
        _logger.LogInformation("DocketOrchestrationService stopped");
        
        return Task.CompletedTask;
    }

    private void HandleDocketLoaded(MonitorInfoEventArgs e)
    {
        _logger.LogInformation("Docket loaded: {DocketName} (Type: {DocketType})", 
            e.Docket.Name, e.Docket.Type);

        // Docket is now in DocketManager - no need to register endpoints here
        // Routing happens via catch-all endpoint that queries DocketManager
    }

    private void HandleDocketUnloaded(MonitorInfoEventArgs e)
    {
        _logger.LogInformation("Docket unloaded: {DocketName}", e.Docket.Name);
        
        // Docket removed from DocketManager - catch-all will no longer match it
    }

    private void HandleDocketReloaded(MonitorInfoEventArgs e)
    {
        _logger.LogInformation("Docket reloaded: {DocketName}", e.Docket.Name);
        
        // Docket updated in DocketManager
    }

    private void HandleDocketError(MonitorErrorEventArgs e)
    {
        _logger.LogError("DocketMonitor error: {ErrorCode} - {ErrorDetails}", 
            e.Error.Code, e.Error.Details);
    }
}


