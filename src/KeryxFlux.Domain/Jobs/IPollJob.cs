namespace KeryxFlux.Domain.Jobs;

/// <summary>
/// Contract for polling jobs that are scheduled via Hangfire.
/// Implementations execute specific polling workflows (e.g., tenant/endpoint combinations).
/// </summary>
public interface IPollJob
{
    /// <summary>
    /// Unique identifier for this job type
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Execute the polling job
    /// </summary>
    /// <param name="jobId">Unique job instance identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success/failure and items processed</returns>
    Task<PollJobResult> ExecuteAsync(string jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a polling job execution
/// </summary>
public sealed class PollJobResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public int ItemsProcessed { get; }
    public int DestinationsForwarded { get; }
    public TimeSpan Duration { get; }

    private PollJobResult(
        bool isSuccess,
        string? errorMessage,
        int itemsProcessed,
        int destinationsForwarded,
        TimeSpan duration)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ItemsProcessed = itemsProcessed;
        DestinationsForwarded = destinationsForwarded;
        Duration = duration;
    }

    public static PollJobResult Success(int itemsProcessed, int destinationsForwarded, TimeSpan duration)
        => new(true, null, itemsProcessed, destinationsForwarded, duration);

    public static PollJobResult Failure(string errorMessage, TimeSpan duration)
        => new(false, errorMessage, 0, 0, duration);
}
