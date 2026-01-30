using KeryxFlux.Domain.Models.Dockets;
using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Represents an API endpoint to poll in a multi-tenant configuration.
/// Endpoints can be combined with tenants to create a polling matrix.
/// </summary>
public sealed class EndpointConfiguration
{
    /// <summary>
    /// Unique identifier for this endpoint (e.g., resource type, endpoint name)
    /// </summary>
    [YamlMember(Alias = "endpoint_id")]
    public required string EndpointId { get; init; }

    /// <summary>
    /// Human-readable display name for this endpoint
    /// </summary>
    [YamlMember(Alias = "display_name")]
    public string? DisplayName { get; init; }

    /// <summary>
    /// Endpoint-specific configuration values.
    /// Example: resource_type, api_version, filters
    /// </summary>
    [YamlMember(Alias = "configuration")]
    public Dictionary<string, string>? Configuration { get; init; }

    /// <summary>
    /// Pagination configuration specific to this endpoint
    /// </summary>
    [YamlMember(Alias = "pagination")]
    public PaginationConfiguration? Pagination { get; init; }

    /// <summary>
    /// Whether this endpoint is enabled for polling (default: true)
    /// </summary>
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; init; } = true;
}
