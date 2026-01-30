using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Application.Services;

/// <summary>
/// Expands a multi-tenant docket into individual tenant/endpoint jobs.
/// Handles configuration merging, overrides, and job matrix generation.
/// </summary>
public sealed class MultiTenantExpansionService
{
    private readonly ILogger<MultiTenantExpansionService> _logger;

    public MultiTenantExpansionService(ILogger<MultiTenantExpansionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Expand a multi-tenant docket into individual polling jobs
    /// </summary>
    public List<TenantEndpointJob> ExpandDocket(Docket docket)
    {
        if (!docket.IsMultiTenant)
        {
            _logger.LogDebug("Docket {DocketName} is not multi-tenant, skipping expansion", docket.Name);
            return new List<TenantEndpointJob>();
        }

        var jobs = new List<TenantEndpointJob>();

        _logger.LogInformation(
            "Expanding multi-tenant docket {DocketName}: {TenantCount} tenants × {EndpointCount} endpoints",
            docket.Name,
            docket.Tenants!.Count,
            docket.Endpoints!.Count
        );

        foreach (var tenant in docket.Tenants!)
        {
            if (!tenant.Enabled)
            {
                _logger.LogDebug("Skipping disabled tenant: {TenantId}", tenant.TenantId);
                continue;
            }

            foreach (var endpoint in docket.Endpoints!)
            {
                if (!endpoint.Enabled)
                {
                    _logger.LogDebug("Skipping disabled endpoint: {EndpointId}", endpoint.EndpointId);
                    continue;
                }

                // Check for override
                var override_ = GetOverride(docket, tenant.TenantId, endpoint.EndpointId);

                // Check if this specific combination is disabled
                if (override_?.Enabled == false)
                {
                    _logger.LogInformation(
                        "Skipping {TenantId}/{EndpointId} (disabled via override)",
                        tenant.TenantId,
                        endpoint.EndpointId
                    );
                    continue;
                }

                // Create job
                var job = CreateJob(docket, tenant, endpoint, override_);
                jobs.Add(job);

                _logger.LogDebug(
                    "Created job {JobId} with cron: {CronExpression}",
                    job.JobId,
                    job.CronExpression
                );
            }
        }

        _logger.LogInformation(
            "Expanded docket {DocketName} into {JobCount} jobs",
            docket.Name,
            jobs.Count
        );

        return jobs;
    }

    /// <summary>
    /// Create a single tenant/endpoint job with merged configuration
    /// </summary>
    private TenantEndpointJob CreateJob(
        Docket docket,
        TenantConfiguration tenant,
        EndpointConfiguration endpoint,
        TenantEndpointOverride? override_)
    {
        // Merge configurations: base ? tenant ? endpoint ? override
        var mergedConfig = MergeConfigurations(
            docket.BaseConfiguration,
            tenant.Configuration,
            endpoint.Configuration,
            override_?.Configuration
        );

        // Add tenant/endpoint identifiers to config
        mergedConfig["tenant_id"] = tenant.TenantId;
        mergedConfig["endpoint_id"] = endpoint.EndpointId;

        // Optional: Add display names
        if (!string.IsNullOrEmpty(tenant.DisplayName))
        {
            mergedConfig["tenant_display_name"] = tenant.DisplayName;
        }
        if (!string.IsNullOrEmpty(endpoint.DisplayName))
        {
            mergedConfig["endpoint_display_name"] = endpoint.DisplayName;
        }

        // Determine cron expression: override ? tenant ? global
        var cronExpression = override_?.CronExpression
            ?? tenant.CronExpression
            ?? docket.Scheduler?.CronExpression
            ?? throw new InvalidOperationException($"No cron expression found for {tenant.TenantId}/{endpoint.EndpointId}");

        // Determine pagination: override ? endpoint ? global
        var pagination = override_?.Pagination
            ?? endpoint.Pagination
            ?? docket.Scheduler?.Pagination;

        // Determine forwarding: override ? global
        var forwarding = override_?.Forwarding
            ?? docket.Forwarding;

        return new TenantEndpointJob
        {
            JobId = $"{tenant.TenantId}-{endpoint.EndpointId}",
            DocketName = docket.Name,
            TenantId = tenant.TenantId,
            EndpointId = endpoint.EndpointId,
            Configuration = mergedConfig,
            Pagination = pagination,
            CronExpression = cronExpression,
            Forwarding = forwarding,
            PluginLocation = docket.PluginLocation,
            Server = docket.Scheduler!.Server,
            Enabled = true
        };
    }

    /// <summary>
    /// Merge multiple configuration dictionaries with priority
    /// Later dictionaries override earlier ones
    /// </summary>
    private Dictionary<string, string> MergeConfigurations(params Dictionary<string, string>?[] configs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var config in configs)
        {
            if (config == null) continue;

            foreach (var (key, value) in config)
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Get override for specific tenant/endpoint combination
    /// </summary>
    private TenantEndpointOverride? GetOverride(Docket docket, string tenantId, string endpointId)
    {
        if (docket.Overrides == null) return null;

        return docket.Overrides.FirstOrDefault(o =>
            o.TenantId.Equals(tenantId, StringComparison.OrdinalIgnoreCase) &&
            o.EndpointId.Equals(endpointId, StringComparison.OrdinalIgnoreCase));
    }
}
