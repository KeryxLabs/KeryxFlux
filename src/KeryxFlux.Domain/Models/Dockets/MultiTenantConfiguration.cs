using YamlDotNet.Serialization;

namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Configuration for multi-tenant polling behavior.
/// Controls parallelism and execution strategy for tenant/endpoint matrix.
/// </summary>
public sealed class MultiTenantConfiguration
{
    /// <summary>
    /// Maximum number of tenant/endpoint jobs to run in parallel.
    /// 0 = unlimited (not recommended), default = 10
    /// </summary>
    [YamlMember(Alias = "max_parallel_jobs")]
    public int MaxParallelJobs { get; init; } = 10;

    /// <summary>
    /// Whether to stop all jobs if one fails (default: false - partial success allowed)
    /// </summary>
    [YamlMember(Alias = "fail_fast")]
    public bool FailFast { get; init; } = false;

    /// <summary>
    /// Delay between starting each job (in milliseconds) to avoid thundering herd
    /// </summary>
    [YamlMember(Alias = "job_start_delay_ms")]
    public int JobStartDelayMs { get; init; } = 0;
}
