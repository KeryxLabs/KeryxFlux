using KeryxFlux.Contracts;
using KeryxFlux.Domain.Ports;
using MediatR;

namespace KeryxFlux.Application.Commands;

/// <summary>
/// Command to process a multi-step polling workflow
/// </summary>
public sealed record ProcessMultiStepCommand : IRequest<ProcessMessageResult>
{
    /// <summary>
    /// Name of the docket executing this workflow
    /// </summary>
    public required string DocketName { get; init; }

    /// <summary>
    /// The received message (from initial poll or subsequent step)
    /// </summary>
    public required ReceivedMessage Message { get; init; }

    /// <summary>
    /// Accumulated state from previous steps (null for first step)
    /// </summary>
    public AccumulatedState? AccumulatedState { get; init; }

    /// <summary>
    /// Current step name (for tracking)
    /// </summary>
    public string? CurrentStepName { get; init; }
}
