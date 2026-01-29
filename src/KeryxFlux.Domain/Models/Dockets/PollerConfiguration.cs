using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Configuration for polling external sources (for poller-type dockets)
/// </summary>
public sealed class PollerConfiguration
{
    /// <summary>
    /// Cron expression for scheduling (e.g., "*/15 * * * *" for every 15 minutes)
    /// </summary>
    [YamlMember(Alias = "cron_expression")]
    public required string CronExpression { get; init; }

    /// <summary>
    /// Hangfire queue name
    /// </summary>
    [YamlMember(Alias = "queue")]
    public string Queue { get; init; } = "default";

    /// <summary>
    /// Server to poll from
    /// </summary>
    [YamlMember(Alias = "server")]
    public required PollerServerConfiguration Server { get; init; }
}

/// <summary>
/// Server configuration for pollers
/// </summary>
public sealed class PollerServerConfiguration
{
    [YamlMember(Alias = "name")]
    public required string Name { get; init; }
    
    [YamlMember(Alias = "address")]
    public required string Address { get; init; }
    
    [YamlMember(Alias = "type")]
    public string Type { get; init; } = "http"; // http, sftp, database
    
    [YamlMember(Alias = "authentication")]
    public AuthenticationConfiguration? Authentication { get; init; }
    
    [YamlMember(Alias = "timeout_seconds")]
    public int TimeoutSeconds { get; init; } = 30;
}

