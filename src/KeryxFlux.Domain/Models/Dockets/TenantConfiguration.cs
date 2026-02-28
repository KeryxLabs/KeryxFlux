using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Represents a tenant/organization in a multi-tenant polling configuration.
/// Tenants share the same endpoint configuration but have unique identifiers.
/// </summary>
public sealed class TenantConfiguration
{
    /// <summary>
    /// Unique identifier for this tenant (e.g., organization code, location ID)
    /// </summary>
    [YamlMember(Alias = "tenant_id")]
    public required string TenantId { get; init; }

    /// <summary>
    /// Human-readable display name for this tenant
    /// </summary>
    [YamlMember(Alias = "display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Tenant-specific configuration values that override base configuration.
    /// Example: region, priority, custom endpoints
    /// </summary>
    [YamlMember(Alias = "configuration")]
    public Dictionary<string, string>? Configuration { get; init; }

    /// <summary>
    /// Tenant-specific date variables (overrides docket-level date variables)
    /// </summary>
    [YamlMember(Alias = "date_variables")]
    public List<DateVariableConfiguration>? DateVariables { get; init; }

    /// <summary>
    /// Override the global cron schedule for this specific tenant
    /// </summary>
    [YamlMember(Alias = "cron_expression")]
    public string? CronExpression { get; init; }

    /// <summary>
    /// Whether this tenant is enabled for polling (default: true)
    /// </summary>
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; init; } = true;
}
