using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Represents an override for a specific tenant/endpoint combination.
/// Allows fine-grained control over specific polling scenarios.
/// </summary>
public sealed class TenantEndpointOverride
{
    /// <summary>
    /// Tenant ID this override applies to
    /// </summary>
    [YamlMember(Alias = "tenant_id")]
    public required string TenantId { get; init; }

    /// <summary>
    /// Endpoint ID this override applies to
    /// </summary>
    [YamlMember(Alias = "endpoint_id")]
    public required string EndpointId { get; init; }

    /// <summary>
    /// Whether this specific tenant/endpoint combination is enabled
    /// </summary>
    [YamlMember(Alias = "enabled")]
    public bool? Enabled { get; init; }

    /// <summary>
    /// Override configuration for this specific combination
    /// </summary>
    [YamlMember(Alias = "configuration")]
    public Dictionary<string, string>? Configuration { get; init; }

    /// <summary>
    /// Override pagination configuration
    /// </summary>
    [YamlMember(Alias = "pagination")]
    public PaginationConfiguration? Pagination { get; init; }

    /// <summary>
    /// Override cron schedule for this specific combination
    /// </summary>
    [YamlMember(Alias = "cron_expression")]
    public string? CronExpression { get; init; }

    /// <summary>
    /// Override forwarding destinations for this combination
    /// </summary>
    [YamlMember(Alias = "forwarding")]
    public ForwardingConfiguration? Forwarding { get; init; }
}
