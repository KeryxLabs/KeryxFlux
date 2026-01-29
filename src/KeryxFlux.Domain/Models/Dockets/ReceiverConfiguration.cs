using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Configuration for receiving data (for receiver-type dockets)
/// </summary>
public sealed class ReceiverConfiguration
{
    /// <summary>
    /// Type of receiver (http, rabbitmq, kafka, tcp)
    /// </summary>
    [YamlMember(Alias = "type")]
    public required string Type { get; init; }

    /// <summary>
    /// HTTP endpoint path (for HTTP receivers)
    /// </summary>
    [YamlMember(Alias = "endpoint")]
    public string? Endpoint { get; init; }

    /// <summary>
    /// Queue/Topic name (for message queue receivers)
    /// </summary>
    [YamlMember(Alias = "queue_or_topic")]
    public string? QueueOrTopic { get; init; }

    /// <summary>
    /// TCP port (for TCP receivers)
    /// </summary>
    [YamlMember(Alias = "port")]
    public int? Port { get; init; }

    /// <summary>
    /// Authentication configuration
    /// </summary>
    [YamlMember(Alias = "authentication")]
    public AuthenticationConfiguration? Authentication { get; init; }

    /// <summary>
    /// Rate limiting configuration
    /// </summary>
    [YamlMember(Alias = "rate_limiting")]
    public RateLimitConfiguration? RateLimiting { get; init; }

    /// <summary>
    /// Processing configuration
    /// </summary>
    [YamlMember(Alias = "processing")]
    public ProcessingConfiguration? Processing { get; init; }
}

/// <summary>
/// Authentication configuration for receivers
/// </summary>
public sealed class AuthenticationConfiguration
{
    [YamlMember(Alias = "type")]
    public required string Type { get; init; } // api_key, oauth2, basic, none
    
    [YamlMember(Alias = "header")]
    public string? Header { get; init; }
    
    [YamlMember(Alias = "secret_env")]
    public string? SecretEnv { get; init; }
    
    [YamlMember(Alias = "token_url")]
    public string? TokenUrl { get; init; }
    
    [YamlMember(Alias = "client_id_env")]
    public string? ClientIdEnv { get; init; }
    
    [YamlMember(Alias = "client_secret_env")]
    public string? ClientSecretEnv { get; init; }
}

/// <summary>
/// Rate limiting configuration
/// </summary>
public sealed class RateLimitConfiguration
{
    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; init; }
    
    [YamlMember(Alias = "requests_per_minute")]
    public int RequestsPerMinute { get; init; }
    
    [YamlMember(Alias = "burst_size")]
    public int BurstSize { get; init; }
}

/// <summary>
/// Processing configuration
/// </summary>
public sealed class ProcessingConfiguration
{
    [YamlMember(Alias = "mode")]
    public string Mode { get; init; } = "async"; // async, sync
    
    [YamlMember(Alias = "timeout_seconds")]
    public int TimeoutSeconds { get; init; } = 30;
    
    [YamlMember(Alias = "max_concurrent")]
    public int MaxConcurrent { get; init; } = 10;
}

