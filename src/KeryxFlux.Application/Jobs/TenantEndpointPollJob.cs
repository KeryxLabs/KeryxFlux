using KeryxFlux.Application.Commands;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Jobs;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace KeryxFlux.Application.Jobs;

/// <summary>
/// Hangfire job that executes a single tenant/endpoint polling workflow.
/// Triggered by Hangfire scheduler based on cron expression.
/// </summary>
public sealed class TenantEndpointPollJob : IPollJob
{
    public string JobType => "TenantEndpointPoll";

    private readonly IDocketManager _docketManager;
    private readonly IMediator _mediator;
    private readonly ILogger<TenantEndpointPollJob> _logger;

    public TenantEndpointPollJob(
        IDocketManager docketManager,
        IMediator mediator,
        ILogger<TenantEndpointPollJob> logger)
    {
        _docketManager = docketManager;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Execute the tenant/endpoint polling job
    /// </summary>
    /// <param name="jobId">Job ID in format: {DocketName}::{TenantId}-{EndpointId}</param>
    public async Task<PollJobResult> ExecuteAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Parse job ID: "healthsystem-multi-tenant::MAIN-Patient"
            var parts = jobId.Split("::");
            if (parts.Length != 2)
            {
                _logger.LogError("Invalid job ID format: {JobId}. Expected format: DocketName::TenantId-EndpointId", jobId);
                return PollJobResult.Failure($"Invalid job ID format: {jobId}", stopwatch.Elapsed);
            }

            var docketName = parts[0];
            var tenantEndpointId = parts[1];

            _logger.LogInformation(
                "Starting poll job: {JobId} (Docket: {DocketName}, TenantEndpoint: {TenantEndpoint})",
                jobId,
                docketName,
                tenantEndpointId
            );

            // Get docket
            var docket = _docketManager.GetDocketByName(docketName);
            if (docket == null)
            {
                _logger.LogError("Docket {DocketName} not found for job {JobId}", docketName, jobId);
                return PollJobResult.Failure($"Docket '{docketName}' not found", stopwatch.Elapsed);
            }

            // For multi-tenant dockets, we need to find the specific tenant/endpoint job
            // The job ID contains the tenant-endpoint identifier
            // The actual polling will be done via ProcessMultiStepCommand

            // Create command to process this specific tenant/endpoint
            var command = new ProcessMultiStepCommand
            {
                DocketName = docketName,
                Message = new ReceivedMessage
                {
                    Payload = Array.Empty<byte>(),
                    CorrelationId = Guid.NewGuid().ToString(),
                    Metadata = new Dictionary<string, string>
                    {
                        { "job_id", jobId },
                        { "tenant_endpoint_id", tenantEndpointId },
                        { "triggered_by", "hangfire" },
                        { "triggered_at", DateTimeOffset.UtcNow.ToString("O") }
                    }
                },
                // Store the tenant/endpoint context for the handler
                CurrentStepName = tenantEndpointId
            };

            // Execute via mediator
            var result = await _mediator.Send(command, cancellationToken);

            stopwatch.Stop();

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Poll job {JobId} completed successfully: {DestinationsForwarded} destinations forwarded in {Duration}ms",
                    jobId,
                    result.DestinationsForwarded,
                    stopwatch.ElapsedMilliseconds
                );

                return PollJobResult.Success(
                    result.DestinationsForwarded,
                    result.DestinationsForwarded,
                    stopwatch.Elapsed
                );
            }
            else
            {
                _logger.LogError(
                    "Poll job {JobId} failed: {ErrorMessage}",
                    jobId,
                    result.ErrorMessage
                );

                return PollJobResult.Failure(result.ErrorMessage ?? "Unknown error", stopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(
                ex,
                "Unexpected error executing poll job {JobId} after {Duration}ms",
                jobId,
                stopwatch.ElapsedMilliseconds
            );

            return PollJobResult.Failure($"Unexpected error: {ex.Message}", stopwatch.Elapsed);
        }
    }
}

