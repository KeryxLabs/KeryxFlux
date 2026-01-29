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
    /// Server information for pollers (legacy support, prefer Scheduler.Server)
    /// </summary>
    [YamlMember(Alias = "server_information")]
    public ServerInformation? ServerInformation { get; init; }

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
