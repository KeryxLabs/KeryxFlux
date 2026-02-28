using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;

namespace KeryxFlux.Application.Commands;

/// <summary>
/// Command for processing messages through model-enhanced plugins.
/// Routes to ProcessModelCommandHandler which orchestrates model invocations.
/// </summary>
public sealed class ProcessModelCommand : IRequest<ProcessMessageResult>
{
    public required string DocketName { get; init; }
    public required ReceivedMessage Message { get; init; }
}
