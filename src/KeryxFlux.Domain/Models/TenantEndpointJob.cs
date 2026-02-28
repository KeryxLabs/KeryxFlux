using KeryxFlux.Domain.Models.Dockets;

namespace KeryxFlux.Domain.Models;

/// <summary>
/// Represents a single tenant/endpoint polling job.
/// Created by expanding a multi-tenant docket into individual executable jobs.
/// </summary>
public sealed class TenantEndpointJob
{
    /// <summary>
    /// Unique identifier for this job (e.g., "ORG_MAIN-Runs")
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Parent docket name this job belongs to
    /// </summary>
    public required string DocketName { get; init; }

    /// <summary>
    /// Tenant ID (organization identifier)
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Endpoint ID (resource type, API endpoint, etc.)
    /// </summary>
    public required string EndpointId { get; init; }

    /// <summary>
    /// Merged configuration for this specific tenant/endpoint combination.
    /// Priority: base ? tenant ? endpoint ? override
    /// </summary>
    public required IReadOnlyDictionary<string, string> Configuration { get; init; }

    /// <summary>
    /// Pagination configuration for this job
    /// </summary>
    public PaginationConfiguration? Pagination { get; init; }

    /// <summary>
    /// Cron expression for scheduling this job
    /// </summary>
    public required string CronExpression { get; init; }

    /// <summary>
    /// Forwarding configuration for this job
    /// </summary>
    public required ForwardingConfiguration Forwarding { get; init; }

    /// <summary>
    /// Plugin location
    /// </summary>
    public required string PluginLocation { get; init; }

    /// <summary>
    /// Server configuration
    /// </summary>
    public required PollerServerConfiguration Server { get; init; }

    /// <summary>
    /// Whether this job is enabled
    /// </summary>
    public bool Enabled { get; init; } = true;
}
