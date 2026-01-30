using KeryxFlux.Domain.Models.Dockets;
using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models;

/// <summary>
/// Represents a complete docket configuration (workflow definition).
/// A docket defines how to receive/poll data, transform it, and forward it to destinations.
/// </summary>
public sealed class Docket
{
    /// <summary>
    /// Unique name for this docket
    /// </summary>
    [YamlMember(Alias = "name")]
    public required string Name { get; init; }

    /// <summary>
    /// Semantic version of this docket configuration
    /// </summary>
    [YamlMember(Alias = "version")]
    public required string Version { get; init; }

    /// <summary>
    /// Type of docket (receiver or poller)
    /// </summary>
    [YamlMember(Alias = "type")]
    public required DocketType Type { get; init; }

    /// <summary>
    /// Path to the transformation plugin DLL
    /// </summary>
    [YamlMember(Alias = "plugin_location")]
    public required string PluginLocation { get; init; }

    /// <summary>
    /// Receiver configuration (only for receiver-type dockets)
    /// </summary>
    [YamlMember(Alias = "receiver")]
    public ReceiverConfiguration? Receiver { get; init; }

    /// <summary>
    /// Poller/scheduler configuration (only for poller-type dockets)
    /// </summary>
    [YamlMember(Alias = "scheduler")]
    public PollerConfiguration? Scheduler { get; init; }

    /// <summary>
    /// Forwarding configuration (where to send transformed data)
    /// </summary>
    [YamlMember(Alias = "forwarding")]
    public required ForwardingConfiguration Forwarding { get; init; }

    /// <summary>
    /// Telemetry and monitoring configuration
    /// </summary>
    [YamlMember(Alias = "telemetry")]
    public TelemetryConfiguration? Telemetry { get; init; }

    /// <summary>
    /// Configuration values for templating and dynamic behavior.
    /// Example: business_id, environment, api_version, etc.
    /// Used for path templating like: /api/{environment}/patients/{business_id}
    /// </summary>
    [YamlMember(Alias = "configuration")]
    public Dictionary<string, string>? Configuration { get; init; }

    // ===== Multi-Tenant Polling Configuration =====

    /// <summary>
    /// Base configuration shared across all tenants/endpoints.
    /// Individual tenant/endpoint configs can override these values.
    /// </summary>
    [YamlMember(Alias = "base_configuration")]
    public Dictionary<string, string>? BaseConfiguration { get; init; }

    /// <summary>
    /// List of tenants (facilities, locations, organizations) to poll.
    /// When combined with endpoints, creates a polling matrix.
    /// </summary>
    [YamlMember(Alias = "tenants")]
    public List<TenantConfiguration>? Tenants { get; init; }

    /// <summary>
    /// List of endpoints (resources, APIs) to poll for each tenant.
    /// Creates a tenant × endpoint matrix for parallel polling.
    /// </summary>
    [YamlMember(Alias = "endpoints")]
    public List<EndpointConfiguration>? Endpoints { get; init; }

    /// <summary>
    /// Overrides for specific tenant/endpoint combinations.
    /// Allows fine-grained control (disable, custom config, custom schedule, etc.)
    /// </summary>
    [YamlMember(Alias = "overrides")]
    public List<TenantEndpointOverride>? Overrides { get; init; }

    /// <summary>
    /// Multi-tenant execution configuration (parallelism, error handling, etc.)
    /// </summary>
    [YamlMember(Alias = "multi_tenant")]
    public MultiTenantConfiguration? MultiTenant { get; init; }

    /// <summary>
    /// Whether this docket uses multi-tenant configuration
    /// </summary>
    public bool IsMultiTenant => Tenants != null && Tenants.Count > 0 && Endpoints != null && Endpoints.Count > 0;

    /// <summary>
    /// Validate that the docket configuration is internally consistent
    /// </summary>

    public bool IsValid(out string? validationError)
    {
        if (Type == DocketType.Receiver && Receiver == null)
        {
            validationError = "Receiver configuration is required for receiver-type dockets";
            return false;
        }

        if (Type == DocketType.Poller && Scheduler == null)
        {
            validationError = "Scheduler configuration is required for poller-type dockets";
            return false;
        }

        if (Forwarding?.Destinations?.Count == 0)
        {
            validationError = "At least one forwarding destination is required";
            return false;

        }

        validationError = null;
        return true;
    }
}
