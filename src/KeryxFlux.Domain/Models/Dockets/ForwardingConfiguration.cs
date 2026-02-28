using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Configuration for forwarding transformed data to destinations
/// </summary>
public sealed class ForwardingConfiguration
{
    /// <summary>
    /// List of destinations to forward to
    /// </summary>
    [YamlMember(Alias = "destinations")]
    public List<DestinationConfiguration> Destinations { get; init; } = [];
}

/// <summary>
/// Configuration for a single forwarding destination
/// </summary>
public sealed class DestinationConfiguration
{
    /// <summary>
    /// Unique name for this destination
    /// </summary>
    [YamlMember(Alias = "name")]
    public required string Name { get; init; }

    /// <summary>
    /// Type of destination (http, rabbitmq, kafka, tcp, sftp)
    /// </summary>
    [YamlMember(Alias = "type")]
    public required string Type { get; init; }

    // HTTP-specific
    [YamlMember(Alias = "url")]
    public string? Url { get; init; }
    
    [YamlMember(Alias = "method")]
    public string? Method { get; init; }
    
    [YamlMember(Alias = "headers")]
    public Dictionary<string, string>? Headers { get; init; }

    // Message Queue-specific
    [YamlMember(Alias = "bootstrap_servers")]
    public string? BootstrapServers { get; init; }
    
    [YamlMember(Alias = "connection_string")]
    public string? ConnectionString { get; init; }
    
    [YamlMember(Alias = "connection_env")]
    public string? ConnectionEnv { get; init; }
    
    [YamlMember(Alias = "exchange")]
    public string? Exchange { get; init; }
    
    [YamlMember(Alias = "exchange_name")]
    public string? ExchangeName { get; init; }
    
    [YamlMember(Alias = "exchange_type")]
    public string? ExchangeType { get; init; }
    
    [YamlMember(Alias = "queue_name")]
    public string? QueueName { get; init; }
    
    [YamlMember(Alias = "routing_key")]
    public string? RoutingKey { get; init; }
    
    [YamlMember(Alias = "topic")]
    public string? Topic { get; init; }
    
    [YamlMember(Alias = "partition_key")]
    public string? PartitionKey { get; init; }
    
    [YamlMember(Alias = "durable")]
    public bool? Durable { get; init; } = true;
    
    // gRPC-specific
    [YamlMember(Alias = "grpc_endpoint")]
    public string? GrpcEndpoint { get; init; }
    
    [YamlMember(Alias = "service_name")]
    public string? ServiceName { get; init; }
    
    [YamlMember(Alias = "method_name")]
    public string? MethodName { get; init; }
    
    [YamlMember(Alias = "use_tls")]
    public bool? UseTls { get; init; } = true;

    // TCP-specific
    [YamlMember(Alias = "host")]
    public string? Host { get; init; }
    
    [YamlMember(Alias = "port")]
    public int? Port { get; init; }
    
    [YamlMember(Alias = "framing")]
    public string? Framing { get; init; } = "mllp";
    
    [YamlMember(Alias = "delimiter")]
    public string? Delimiter { get; init; } = "\n";
    
    [YamlMember(Alias = "persistent")]
    public bool? Persistent { get; init; } = true;

    // Common
    [YamlMember(Alias = "timeout_seconds")]
    public int TimeoutSeconds { get; init; } = 30;
    
    [YamlMember(Alias = "retry_policy")]
    public RetryPolicyConfiguration? RetryPolicy { get; init; }
}

/// <summary>
/// Retry policy configuration
/// </summary>
public sealed class RetryPolicyConfiguration
{
    [YamlMember(Alias = "max_attempts")]
    public int MaxAttempts { get; init; } = 3;
    
    [YamlMember(Alias = "backoff_strategy")]
    public string BackoffStrategy { get; init; } = "exponential"; // exponential, linear, constant
    
    [YamlMember(Alias = "initial_delay_seconds")]
    public int InitialDelaySeconds { get; init; } = 1;
    
    [YamlMember(Alias = "max_delay_seconds")]
    public int MaxDelaySeconds { get; init; } = 60;
}

