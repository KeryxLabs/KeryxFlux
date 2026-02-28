using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Infrastructure.Receivers;

/// <summary>
/// HTTP receiver implementation.
/// Actual HTTP routing is handled by catch-all endpoint in Host layer.
/// This class exists to satisfy IReceiver interface for DI registration.
/// </summary>
public class HttpReceiver : IReceiver
{
    public string Type => "http";

    private readonly ILogger<HttpReceiver> _logger;

    public HttpReceiver(ILogger<HttpReceiver> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HTTP Receiver initialized");
        // HTTP receiver uses ASP.NET Core endpoints - no background work needed
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HTTP Receiver stopped");
        return Task.CompletedTask;
    }
}








