using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Ports;
using MediatR;

namespace KeryxFlux.Host.Extensions;

/// <summary>
/// Extension methods for configuring KeryxFlux endpoints.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps all KeryxFlux endpoints including catch-all receiver routing,
    /// health checks, and administrative endpoints.
    /// </summary>
    public static WebApplication MapKeryxFluxEndpoints(this WebApplication app)
    {
        // Catch-all endpoint for dynamic receiver routing
        app.MapPost("/{**path}", async (
            string path,
            HttpContext context,
            IDocketManager docketManager,
            IMediator mediator) =>
        {
            app.Logger.LogDebug("Received request for path: /{Path}", path);

            // Find docket matching this path
            var docket = docketManager.GetReceiverDockets()
                .FirstOrDefault(d => d.Receiver?.Endpoint?.TrimStart('/') == path ||
                                    $"receive/{d.Name}" == path);

            if (docket == null)
            {
                app.Logger.LogWarning("No docket found for path: /{Path}", path);
                return Results.NotFound(new { error = "No receiver configured for this path" });
            }

            app.Logger.LogInformation("Processing request for docket {DocketName}", docket.Name);

            // Read request body
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var payload = System.Text.Encoding.UTF8.GetBytes(body);

            // Extract metadata
            var metadata = new Dictionary<string, string>
            {
                { "method", context.Request.Method },
                { "path", context.Request.Path },
                { "contentType", context.Request.ContentType ?? "application/octet-stream" }
            };

            // Add headers
            foreach (var header in context.Request.Headers)
            {
                metadata[$"header:{header.Key}"] = header.Value.ToString();
            }

            // Create received message
            var message = new ReceivedMessage
            {
                Payload = payload,
                ContentType = context.Request.ContentType ?? "application/octet-stream",
                CorrelationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? Guid.NewGuid().ToString(),
                SourceEndpoint = $"/{path}",
                Metadata = metadata
            };

            // Process through pipeline
            var command = new ProcessMessageCommand
            {
                DocketName = docket.Name,
                Message = message
            };

            var result = await mediator.Send(command);

            if (result.IsSuccess)
            {
                app.Logger.LogInformation(
                    "Successfully processed message for docket {DocketName}. Forwarded to {Count} destinations.",
                    docket.Name,
                    result.DestinationsForwarded
                );

                return Results.Accepted(value: new
                {
                    status = "accepted",
                    docket = docket.Name,
                    correlationId = message.CorrelationId,
                    destinationsForwarded = result.DestinationsForwarded
                });
            }
            else
            {
                app.Logger.LogError("Failed to process message for docket {DocketName}: {Error}",
                    docket.Name, result.ErrorMessage);

                return Results.Problem(
                    detail: result.ErrorMessage,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        })
        .WithName("DynamicReceiverCatchAll")
        .WithTags("Receivers");

        // Root endpoint
        app.MapGet("/", () => new
        {
            service = "KeryxFlux",
            status = "running",
            version = "1.0.0-phase1.5",
            endpoints = new[]
            {
                new { method = "GET", path = "/health", description = "Health check endpoint" },
                new { method = "GET", path = "/dockets", description = "List loaded dockets" }
            }
        })
        .WithName("Root")
        .WithTags("Info");

        // Health endpoint
        app.MapGet("/health", () => new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow
        })
        .WithName("HealthCheck")
        .WithTags("Health");

        // Dockets endpoint
        app.MapGet("/dockets", (IDocketManager docketManager) => new
        {
            receivers = docketManager.GetReceiverDockets().Select(d => new
            {
                d.Name,
                d.Version,
                d.Type,
                endpoint = d.Receiver?.Endpoint ?? $"/receive/{d.Name}",
                plugin = Path.GetFileName(d.PluginLocation)
            }),
            pollers = docketManager.GetPollerDockets().Select(d => new
            {
                d.Name,
                d.Version,
                d.Type,
                schedule = d.Scheduler?.CronExpression,
                plugin = Path.GetFileName(d.PluginLocation)
            })
        })
        .WithName("ListDockets")
        .WithTags("Admin");

        return app;
    }
}
