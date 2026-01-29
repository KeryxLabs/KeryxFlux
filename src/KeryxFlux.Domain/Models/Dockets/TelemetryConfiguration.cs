using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Telemetry and monitoring configuration
/// </summary>
public sealed class TelemetryConfiguration
{
    [YamlMember(Alias = "trace_requests")]
    public bool TraceRequests { get; init; } = true;
    
    [YamlMember(Alias = "log_level")]
    public string LogLevel { get; init; } = "Information";
    
    [YamlMember(Alias = "metrics")]
    public List<string> Metrics { get; init; } = [];
}

