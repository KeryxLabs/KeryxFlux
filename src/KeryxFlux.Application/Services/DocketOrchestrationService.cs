using KeryxFlux.Application.FileSystem;
using KeryxFlux.Application.Jobs;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Application.Services;

/// <summary>
/// Orchestrates docket loading and monitoring as a hosted service.
/// Handles the coordination between DocketMonitor, DocketManager, and Hangfire job registration.
/// Pure orchestration - no web framework coupling.
/// </summary>
public class DocketOrchestrationService : IHostedService
{
    private readonly ILogger<DocketOrchestrationService> _logger;
    private readonly IDocketMonitor _docketMonitor;
    private readonly JobRegistrationService _jobRegistrationService;
    private readonly IServiceProvider _serviceProvider;

    public DocketOrchestrationService(
        ILogger<DocketOrchestrationService> logger,
        IDocketMonitor docketMonitor,
        JobRegistrationService jobRegistrationService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _docketMonitor = docketMonitor;
        _jobRegistrationService = jobRegistrationService;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Called when the host is starting.
    /// Subscribes to docket events and starts monitoring.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting DocketOrchestrationService with Hangfire integration");

        // Subscribe to docket loading events
        _docketMonitor.OnLoaded += (sender, e) => HandleDocketLoaded(e);
        _docketMonitor.OnUnloaded += (sender, e) => HandleDocketUnloaded(e);
        _docketMonitor.OnReloaded += (sender, e) => HandleDocketReloaded(e);
        _docketMonitor.OnError += (sender, e) => HandleDocketError(e);

        // Start monitoring for dockets
        _docketMonitor.Start();

        _logger.LogInformation("DocketOrchestrationService started - Hangfire jobs will be registered automatically");
        
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

        // Register Hangfire jobs for poller dockets
        if (e.Docket.Type == DocketType.Poller)
        {
            _logger.LogInformation("Registering Hangfire jobs for poller docket: {DocketName}", e.Docket.Name);
            _jobRegistrationService.RegisterDocketJobs(e.Docket);
        }
        // Start receivers for receiver dockets
        else if (e.Docket.Type == DocketType.Receiver)
        {
            _logger.LogInformation("Starting receiver for docket: {DocketName}", e.Docket.Name);
            RegisterReceiverForDocket(e.Docket);
        }
    }

    private void HandleDocketUnloaded(MonitorInfoEventArgs e)
    {
        _logger.LogInformation("Docket unloaded: {DocketName}", e.Docket.Name);
        
        // Unregister Hangfire jobs for poller dockets
        if (e.Docket.Type == DocketType.Poller)
        {
            _logger.LogInformation("Unregistering Hangfire jobs for docket: {DocketName}", e.Docket.Name);
            _jobRegistrationService.UnregisterDocketJobs(e.Docket);
        }
        // Unregister receivers for receiver dockets
        else if (e.Docket.Type == DocketType.Receiver)
        {
            _logger.LogInformation("Stopping receiver for docket: {DocketName}", e.Docket.Name);
            UnregisterReceiverForDocket(e.Docket);
        }
    }

    private void HandleDocketReloaded(MonitorInfoEventArgs e)
    {
        _logger.LogInformation("Docket reloaded: {DocketName}", e.Docket.Name);
        
        // Re-register Hangfire jobs (handles config changes)
        if (e.Docket.Type == DocketType.Poller)
        {
            _logger.LogInformation("Re-registering Hangfire jobs for docket: {DocketName}", e.Docket.Name);
            _jobRegistrationService.ReregisterDocketJobs(e.Docket);
        }
    }

    private void HandleDocketError(MonitorErrorEventArgs e)
    {
        _logger.LogError("DocketMonitor error: {ErrorCode} - {ErrorDetails}", 
            e.Error.Code, e.Error.Details);
    }

    private void RegisterReceiverForDocket(Docket docket)
    {
        if (docket.Receiver == null)
        {
            _logger.LogWarning("Docket {DocketName} is type receiver but has no receiver configuration", docket.Name);
            return;
        }

        var receiverType = docket.Receiver.Type.ToLowerInvariant();

        // Handle RabbitMQ receivers
        if (receiverType == "rabbitmq")
        {
            var rabbitMqReceiver = _serviceProvider.GetService<IRabbitMqReceiverService>();
            if (rabbitMqReceiver == null)
            {
                _logger.LogError("IRabbitMqReceiverService not registered");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await rabbitMqReceiver.RegisterConsumerForDocketAsync(docket);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register RabbitMQ consumer for docket {DocketName}", docket.Name);
                }
            });
        }
        // Handle Kafka consumers
        else if (receiverType == "kafka")
        {
            var kafkaConsumer = _serviceProvider.GetService<IKafkaConsumerService>();
            if (kafkaConsumer == null)
            {
                _logger.LogError("IKafkaConsumerService not registered");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await kafkaConsumer.RegisterConsumerForDocketAsync(docket);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to register Kafka consumer for docket {DocketName}", docket.Name);
                }
            });
        }
        // HTTP receivers don't need explicit registration (handled by ASP.NET endpoints)
        else if (receiverType == "http")
        {
            _logger.LogInformation("HTTP receiver for docket {DocketName} uses ASP.NET endpoints - no registration needed", docket.Name);
        }
        else
        {
            _logger.LogWarning("Unknown receiver type {ReceiverType} for docket {DocketName}", receiverType, docket.Name);
        }
    }

    private void UnregisterReceiverForDocket(Docket docket)
    {
        if (docket.Receiver == null) return;

        var receiverType = docket.Receiver.Type.ToLowerInvariant();

        if (receiverType == "rabbitmq")
        {
            var rabbitMqReceiver = _serviceProvider.GetService<IRabbitMqReceiverService>();
            if (rabbitMqReceiver == null) return;

            Task.Run(async () =>
            {
                try
                {
                    await rabbitMqReceiver.UnregisterConsumerForDocketAsync(docket.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unregister RabbitMQ consumer for docket {DocketName}", docket.Name);
                }
            });
        }
        else if (receiverType == "kafka")
        {
            var kafkaConsumer = _serviceProvider.GetService<IKafkaConsumerService>();
            if (kafkaConsumer == null) return;

            Task.Run(async () =>
            {
                try
                {
                    await kafkaConsumer.UnregisterConsumerForDocketAsync(docket.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to unregister Kafka consumer for docket {DocketName}", docket.Name);
                }
            });
        }
    }
}




