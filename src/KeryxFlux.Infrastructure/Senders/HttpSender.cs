using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace KeryxFlux.Infrastructure.Senders;

/// <summary>
/// HTTP sender that forwards messages to HTTP endpoints with retry logic.
/// </summary>
public class HttpSender : ISender
{
    public string Type => "http";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpSender> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

    public HttpSender(IHttpClientFactory httpClientFactory, ILogger<HttpSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Create resilience pipeline with retry strategy (Polly 8.x)
        _resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => !r.IsSuccessStatusCode),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "HTTP request failed (attempt {AttemptNumber}/3). Retrying after {Delay}s. Status: {Status}",
                        args.AttemptNumber,
                        args.RetryDelay.TotalSeconds,
                        args.Outcome.Result?.StatusCode ?? System.Net.HttpStatusCode.InternalServerError
                    );
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<SendResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();

            // Build HTTP request
            var request = new HttpRequestMessage(HttpMethod.Post, message.DestinationName)
            {
                Content = new ByteArrayContent(message.Payload)
            };

            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                message.ContentType ?? "application/octet-stream"
            );

            // Add correlation ID header
            request.Headers.Add("X-Correlation-Id", message.CorrelationId);

            // Set timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (message.Timeout.HasValue)
            {
                cts.CancelAfter(message.Timeout.Value);
            }

            _logger.LogInformation(
                "Sending HTTP request to {Destination} (CorrelationId: {CorrelationId})",
                message.DestinationName,
                message.CorrelationId
            );

            // Execute with retry using resilience pipeline
            var response = await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                return await httpClient.SendAsync(request, ct);
            }, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully sent message to {Destination}. Status: {StatusCode}",
                    message.DestinationName,
                    response.StatusCode
                );

                return SendResult.Success();
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "HTTP request failed to {Destination}. Status: {StatusCode}, Body: {ErrorBody}",
                    message.DestinationName,
                    response.StatusCode,
                    errorBody
                );

                return SendResult.Failure(
                    $"Status: {response.StatusCode}, Body: {errorBody}"
                );
            }
        }
        catch (TaskCanceledException)
        {
            var timeout = message.Timeout?.TotalSeconds ?? 30;
            _logger.LogError(
                "HTTP request to {Destination} timed out after {Timeout}s",
                message.DestinationName,
                timeout
            );

            return SendResult.Failure($"Request timed out after {timeout}s");
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx,
                "HTTP request error sending to {Destination}",
                message.DestinationName
            );

            return SendResult.Failure(httpEx.Message, httpEx);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error sending to {Destination}",
                message.DestinationName
            );

            return SendResult.Failure(ex.Message, ex);
        }
    }
}

