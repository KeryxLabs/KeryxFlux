using Hangfire;
using KeryxFlux.Application.Services;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Application.Jobs;

/// <summary>
/// Registers and manages Hangfire recurring jobs for dockets.
/// Handles both single-tenant and multi-tenant docket job registration.
/// </summary>
public sealed class JobRegistrationService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly MultiTenantExpansionService _multiTenantExpansionService;
    private readonly ILogger<JobRegistrationService> _logger;

    public JobRegistrationService(
        IRecurringJobManager recurringJobManager,
        MultiTenantExpansionService multiTenantExpansionService,
        ILogger<JobRegistrationService> logger)
    {
        _recurringJobManager = recurringJobManager;
        _multiTenantExpansionService = multiTenantExpansionService;
        _logger = logger;
    }

    /// <summary>
    /// Register jobs for a docket (single or multi-tenant)
    /// </summary>
    public void RegisterDocketJobs(Docket docket)
    {
        if (docket.Type != DocketType.Poller)
        {
            _logger.LogDebug("Skipping job registration for non-poller docket: {DocketName}", docket.Name);
            return;
        }

        if (docket.IsMultiTenant)
        {
            RegisterMultiTenantJobs(docket);
        }
        else
        {
            RegisterSingleTenantJob(docket);
        }
    }

    /// <summary>
    /// Register jobs for a multi-tenant docket
    /// </summary>
    private void RegisterMultiTenantJobs(Docket docket)
    {
        _logger.LogInformation(
            "Registering multi-tenant jobs for docket: {DocketName}",
            docket.Name
        );

        // Expand docket into individual tenant/endpoint jobs
        var jobs = _multiTenantExpansionService.ExpandDocket(docket);

        _logger.LogInformation(
            "Expanded docket {DocketName} into {JobCount} jobs",
            docket.Name,
            jobs.Count
        );

        foreach (var job in jobs)
        {
            if (!job.Enabled)
            {
                _logger.LogDebug("Skipping disabled job: {JobId}", job.JobId);
                continue;
            }

            // Create unique Hangfire job ID
            var hangfireJobId = $"{docket.Name}::{job.JobId}";

            _logger.LogInformation(
                "Registering Hangfire job: {HangfireJobId} (Cron: {CronExpression})",
                hangfireJobId,
                job.CronExpression
            );

            // Register recurring job with Hangfire
            _recurringJobManager.AddOrUpdate<TenantEndpointPollJob>(
                recurringJobId: hangfireJobId,
                methodCall: pollJob => pollJob.ExecuteAsync(hangfireJobId, CancellationToken.None),
                cronExpression: job.CronExpression,
                options: new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Utc
                }
            );

            _logger.LogDebug(
                "Registered job {JobId} for tenant {TenantId}, endpoint {EndpointId}",
                hangfireJobId,
                job.TenantId,
                job.EndpointId
            );
        }

        _logger.LogInformation(
            "Successfully registered {JobCount} Hangfire jobs for docket {DocketName}",
            jobs.Count,
            docket.Name
        );
    }

    /// <summary>
    /// Register job for a single-tenant (legacy) docket
    /// </summary>
    private void RegisterSingleTenantJob(Docket docket)
    {
        if (docket.Scheduler == null)
        {
            _logger.LogWarning("Poller docket {DocketName} has no scheduler configuration", docket.Name);
            return;
        }

        var hangfireJobId = $"{docket.Name}::single";

        _logger.LogInformation(
            "Registering single-tenant Hangfire job: {HangfireJobId} (Cron: {CronExpression})",
            hangfireJobId,
            docket.Scheduler.CronExpression
        );

        _recurringJobManager.AddOrUpdate<TenantEndpointPollJob>(
            recurringJobId: hangfireJobId,
            methodCall: pollJob => pollJob.ExecuteAsync(hangfireJobId, CancellationToken.None),
            cronExpression: docket.Scheduler.CronExpression,
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            }
        );

        _logger.LogInformation("Successfully registered job for docket {DocketName}", docket.Name);
    }

    /// <summary>
    /// Remove all jobs associated with a docket
    /// </summary>
    public void UnregisterDocketJobs(Docket docket)
    {
        _logger.LogInformation("Unregistering jobs for docket: {DocketName}", docket.Name);

        if (docket.IsMultiTenant)
        {
            var jobs = _multiTenantExpansionService.ExpandDocket(docket);
            
            foreach (var job in jobs)
            {
                var hangfireJobId = $"{docket.Name}::{job.JobId}";
                _recurringJobManager.RemoveIfExists(hangfireJobId);
                _logger.LogDebug("Removed Hangfire job: {HangfireJobId}", hangfireJobId);
            }
        }
        else
        {
            var hangfireJobId = $"{docket.Name}::single";
            _recurringJobManager.RemoveIfExists(hangfireJobId);
            _logger.LogDebug("Removed Hangfire job: {HangfireJobId}", hangfireJobId);
        }

        _logger.LogInformation("Successfully unregistered jobs for docket {DocketName}", docket.Name);
    }

    /// <summary>
    /// Re-register all jobs for a docket (used when docket is reloaded)
    /// </summary>
    public void ReregisterDocketJobs(Docket docket)
    {
        _logger.LogInformation("Re-registering jobs for docket: {DocketName}", docket.Name);
        
        UnregisterDocketJobs(docket);
        RegisterDocketJobs(docket);
        
        _logger.LogInformation("Successfully re-registered jobs for docket {DocketName}", docket.Name);
    }
}
