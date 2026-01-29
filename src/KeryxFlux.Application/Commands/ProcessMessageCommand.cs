using KeryxFlux.Domain.Ports;
using MediatR;

namespace KeryxFlux.Application.Commands;

/// <summary>
/// Command to process a received message through the transformation pipeline
/// </summary>
public sealed record ProcessMessageCommand : IRequest<ProcessMessageResult>
{
    /// <summary>
    /// Name of the docket that received this message
    /// </summary>
    public required string DocketName { get; init; }

    /// <summary>
    /// The received message to process
    /// </summary>
    public required ReceivedMessage Message { get; init; }
}

/// <summary>
/// Result of processing a message
/// </summary>
public sealed class ProcessMessageResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public int DestinationsForwarded { get; }

    private ProcessMessageResult(bool isSuccess, string? errorMessage, int destinationsForwarded)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        DestinationsForwarded = destinationsForwarded;
    }

    public static ProcessMessageResult Success(int destinationsForwarded) 
        => new(true, null, destinationsForwarded);

    public static ProcessMessageResult Failure(string errorMessage) 
        => new(false, errorMessage, 0);
}
