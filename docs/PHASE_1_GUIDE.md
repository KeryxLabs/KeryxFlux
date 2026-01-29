# Phase 1: First Vertical Slice - Implementation Guide

## Goal
Get HTTP ? Plugin ? HTTP working end-to-end within Week 2.

---

## Architecture Refresher

```
HTTP POST         HttpReceiver        ProcessMessageCommand       PluginManager         HttpSender
   |                    |                     |                         |                    |
   | POST /receive      |                     |                         |                    |
   |?????????????????>  |                     |                         |                    |
   |                    |                     |                         |                    |
   |                    | Send Command        |                         |                    |
   |                    |??????????????????>  |                         |                    |
   |                    |                     |                         |                    |
   |                    |                     | Load Plugin             |                    |
   |                    |                     |??????????????????????>  |                    |
   |                    |                     |                         |                    |
   |                    |                     | <??? IKeryxFluxPlugin   |                    |
   |                    |                     |                         |                    |
   |                    |                     | plugin.Transform(data)  |                    |
   |                    |                     |???????????????????????> |                    |
   |                    |                     |                         |                    |
   |                    |                     | <??? TransformedData    |                    |
   |                    |                     |                         |                    |
   |                    |                     | Forward                 |                    |
   |                    |                     |??????????????????????????????????????????>   |
   |                    |                     |                         |                    |
   |  <?? 202 Accepted  |                     |                         |                    |
   |                    |                     |                         |                    |
```

---

## Task 1: Implement PluginManager.LoadPlugin()

### File: `src/KeryxFlux.Application/Services/PluginManager.cs`

```csharp
private readonly ConcurrentDictionary<string, IKeryxFluxPlugin> _pluginCache = new();
private readonly ConcurrentDictionary<string, AssemblyLoadContext> _loadContexts = new();

public Result<IKeryxFluxPlugin> LoadPlugin(string pluginPath)
{
    // Check cache first
    if (_pluginCache.TryGetValue(pluginPath, out var cachedPlugin))
    {
        return Result.Success(cachedPlugin);
    }

    try
    {
        // Create isolated load context
        var loadContext = new LibraryLoadContext(pluginPath);
        var assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(pluginPath));

        // Find types implementing IKeryxFluxPlugin
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IKeryxFluxPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (pluginType == null)
        {
            return Result.Failure<IKeryxFluxPlugin>(
                new LoadingError($"No type implementing IKeryxFluxPlugin found in {pluginPath}"));
        }

        // Create instance
        var plugin = (IKeryxFluxPlugin)Activator.CreateInstance(pluginType)!;

        // Cache it
        _pluginCache.TryAdd(pluginPath, plugin);
        _loadContexts.TryAdd(pluginPath, loadContext);

        _logger.LogInformation("Loaded plugin {PluginName} v{Version} from {Path}",
            plugin.Name, plugin.Version, pluginPath);

        return Result.Success(plugin);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load plugin from {PluginPath}", pluginPath);
        return Result.Failure<IKeryxFluxPlugin>(
            new LoadingError($"Failed to load plugin: {ex.Message}"));
    }
}
```

---

## Task 2: Create HttpReceiver

### File: `src/KeryxFlux.Infrastructure/Receivers/HttpReceiver.cs`

```csharp
using KeryxFlux.Application.Commands;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KeryxFlux.Infrastructure.Receivers;

public class HttpReceiver : IReceiver
{
    private readonly IMediator _mediator;
    private readonly ILogger<HttpReceiver> _logger;
    private readonly Docket _docket;

    public string Type => "http";

    public HttpReceiver(Docket docket, IMediator mediator, ILogger<HttpReceiver> logger)
    {
        _docket = docket;
        _mediator = mediator;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HttpReceiver for docket {DocketName} started on endpoint {Endpoint}",
            _docket.Name, _docket.Receiver?.Endpoint);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("HttpReceiver for docket {DocketName} stopped", _docket.Name);
        return Task.CompletedTask;
    }

    public async Task<IResult> HandleRequestAsync(HttpContext context)
    {
        try
        {
            // Read request body
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var payload = Encoding.UTF8.GetBytes(body);

            // Extract headers
            var metadata = context.Request.Headers
                .ToDictionary(h => h.Key, h => h.Value.ToString());

            // Create received message
            var message = new ReceivedMessage
            {
                Payload = payload,
                ContentType = context.Request.ContentType ?? "application/octet-stream",
                CorrelationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? Guid.NewGuid().ToString(),
                SourceEndpoint = context.Request.Path,
                Metadata = metadata
            };

            // Send to processing pipeline
            var command = new ProcessMessageCommand
            {
                DocketName = _docket.Name,
                Message = message
            };

            var result = await _mediator.Send(command);

            if (result.IsSuccess)
            {
                return Results.Accepted(null, new
                {
                    correlationId = message.CorrelationId,
                    destinationsForwarded = result.DestinationsForwarded
                });
            }
            else
            {
                return Results.Problem(
                    statusCode: 500,
                    title: "Processing failed",
                    detail: result.ErrorMessage
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling HTTP request for docket {DocketName}", _docket.Name);
            return Results.Problem(statusCode: 500, detail: ex.Message);
        }
    }
}
```

---

## Task 3: Create HttpSender

### File: `src/KeryxFlux.Infrastructure/Senders/HttpSender.cs`

```csharp
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace KeryxFlux.Infrastructure.Senders;

public class HttpSender : ISender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpSender> _logger;
    private readonly DestinationConfiguration _destination;

    public string Type => "http";

    public HttpSender(
        DestinationConfiguration destination,
        HttpClient httpClient,
        ILogger<HttpSender> logger)
    {
        _destination = destination;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Build retry policy if configured
            AsyncRetryPolicy? retryPolicy = null;
            if (_destination.RetryPolicy != null)
            {
                retryPolicy = Policy
                    .Handle<HttpRequestException>()
                    .WaitAndRetryAsync(
                        _destination.RetryPolicy.MaxAttempts,
                        retryAttempt => _destination.RetryPolicy.BackoffStrategy switch
                        {
                            "exponential" => TimeSpan.FromSeconds(
                                Math.Min(
                                    _destination.RetryPolicy.InitialDelaySeconds * Math.Pow(2, retryAttempt - 1),
                                    _destination.RetryPolicy.MaxDelaySeconds
                                )),
                            "linear" => TimeSpan.FromSeconds(
                                _destination.RetryPolicy.InitialDelaySeconds * retryAttempt),
                            _ => TimeSpan.FromSeconds(_destination.RetryPolicy.InitialDelaySeconds)
                        },
                        onRetry: (exception, timeSpan, retryCount, context) =>
                        {
                            _logger.LogWarning(exception,
                                "Retry {RetryCount} for destination {Destination} after {Delay}s",
                                retryCount, _destination.Name, timeSpan.TotalSeconds);
                        });
            }

            // Build HTTP request
            var request = new HttpRequestMessage(
                new HttpMethod(_destination.Method ?? "POST"),
                _destination.Url);

            request.Content = new ByteArrayContent(message.Payload);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(message.ContentType);

            // Add configured headers
            if (_destination.Headers != null)
            {
                foreach (var header in _destination.Headers)
                {
                    // Replace environment variable placeholders
                    var value = header.Value.StartsWith("${") && header.Value.EndsWith("}")
                        ? Environment.GetEnvironmentVariable(header.Value.Trim('$', '{', '}'))
                        : header.Value;

                    request.Headers.TryAddWithoutValidation(header.Key, value);
                }
            }

            // Add correlation ID
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", message.CorrelationId);

            // Send with optional retry
            HttpResponseMessage response;
            if (retryPolicy != null)
            {
                response = await retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.SendAsync(request, cancellationToken));
            }
            else
            {
                response = await _httpClient.SendAsync(request, cancellationToken);
            }

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Successfully sent message to {Destination} ({StatusCode})",
                    _destination.Name, response.StatusCode);
                return SendResult.Success();
            }
            else
            {
                var error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                _logger.LogError("Failed to send to {Destination}: {Error}",
                    _destination.Name, error);
                return SendResult.Failure(error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending to {Destination}", _destination.Name);
            return SendResult.Failure(ex.Message, ex);
        }
    }
}
```

### Dependencies Needed:
```bash
dotnet add src/KeryxFlux.Infrastructure/KeryxFlux.Infrastructure.csproj package Polly
```

---

## Task 4: Create Sample Plugin

### File: `plugins/KeryxFlux.Plugins.Sample/SamplePlugin.cs`

Create new project:
```bash
dotnet new classlib -n KeryxFlux.Plugins.Sample -o plugins/KeryxFlux.Plugins.Sample -f net8.0
dotnet add plugins/KeryxFlux.Plugins.Sample/KeryxFlux.Plugins.Sample.csproj reference src/KeryxFlux.Contracts/KeryxFlux.Contracts.csproj
```

```csharp
using System.Text;
using System.Text.Json;
using KeryxFlux.Contracts;

namespace KeryxFlux.Plugins.Sample;

public class SamplePlugin : IKeryxFluxPlugin
{
    public string Name => "SamplePlugin";
    public string Version => "1.0.0";

    public TransformationResult Transform(byte[] source, TransformationContext context)
    {
        try
        {
            var input = Encoding.UTF8.GetString(source);

            // Simple transformation: Add metadata envelope
            var envelope = new
            {
                receivedAt = context.ReceivedAt,
                docket = context.DocketName,
                correlationId = context.CorrelationId,
                originalData = input,
                processedAt = DateTimeOffset.UtcNow
            };

            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            var output = Encoding.UTF8.GetBytes(json);

            return TransformationResult.Success(output, "application/json");
        }
        catch (Exception ex)
        {
            return TransformationResult.Failure($"Transformation failed: {ex.Message}");
        }
    }
}
```

Build it:
```bash
dotnet build plugins/KeryxFlux.Plugins.Sample/KeryxFlux.Plugins.Sample.csproj
```

---

## Task 5: Update Host Program.cs

### File: `src/KeryxFlux.Host/Program.cs`

```csharp
using KeryxFlux.Application;
using KeryxFlux.Application.Services;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Models.Dockets;
using KeryxFlux.Infrastructure.Receivers;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Register Application services (MediatR, etc.)
builder.Services.AddKeryxFluxApplication();

// Register Domain services
builder.Services.AddSingleton<IDocketManager, DocketManager>();
builder.Services.AddSingleton<IPluginManager, PluginManager>();

// Register Infrastructure services
builder.Services.AddHttpClient(); // For HttpSender

var app = builder.Build();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }));

// Load dockets (for now, hard-coded - will load from YAML in next iteration)
var docketManager = app.Services.GetRequiredService<IDocketManager>();

// Create a sample docket programmatically
var sampleDocket = new Docket
{
    Name = "sample-http-receiver",
    Version = "1.0.0",
    Type = DocketType.Receiver,
    PluginLocation = "./plugins/KeryxFlux.Plugins.Sample/bin/Debug/net8.0/KeryxFlux.Plugins.Sample.dll",
    Receiver = new ReceiverConfiguration
    {
        Type = "http",
        Endpoint = "/receive/sample"
    },
    Forwarding = new ForwardingConfiguration
    {
        Destinations = new List<DestinationConfiguration>
        {
            new()
            {
                Name = "webhook-site",
                Type = "http",
                Url = "https://webhook.site/your-unique-url", // Replace with actual webhook URL
                Method = "POST",
                TimeoutSeconds = 30,
                RetryPolicy = new RetryPolicyConfiguration
                {
                    MaxAttempts = 3,
                    BackoffStrategy = "exponential",
                    InitialDelaySeconds = 1,
                    MaxDelaySeconds = 30
                }
            }
        }
    }
};

// Register HTTP endpoint for this docket
var mediator = app.Services.GetRequiredService<MediatR.IMediator>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var receiver = new HttpReceiver(sampleDocket, mediator, loggerFactory.CreateLogger<HttpReceiver>());

app.MapPost(sampleDocket.Receiver.Endpoint, async (HttpContext context) =>
    await receiver.HandleRequestAsync(context));

app.Logger.LogInformation("KeryxFlux started. HTTP receiver listening on {Endpoint}",
    sampleDocket.Receiver.Endpoint);

app.Run();
```

Add dependencies:
```bash
dotnet add src/KeryxFlux.Host/KeryxFlux.Host.csproj reference src/KeryxFlux.Infrastructure/KeryxFlux.Infrastructure.csproj
dotnet add src/KeryxFlux.Host/KeryxFlux.Host.csproj package MediatR
```

---

## Task 6: Test End-to-End

### 1. Build Everything
```bash
dotnet build
```

### 2. Run the Host
```bash
cd src/KeryxFlux.Host
dotnet run
```

### 3. Send a Test Request
```bash
curl -X POST http://localhost:5000/receive/sample \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: test-123" \
  -d '{"message": "Hello KeryxFlux!"}'
```

### Expected Response:
```json
{
  "correlationId": "test-123",
  "destinationsForwarded": 1
}
```

### 4. Check Webhook.site
You should see the transformed payload with metadata envelope.

---

## Success Criteria

- [  ] Plugin loads successfully
- [  ] HTTP POST to `/receive/sample` returns 202 Accepted
- [  ] Transformation occurs (envelope added)
- [  ] Data forwarded to webhook.site
- [  ] Logs show all steps
- [  ] No errors in console

---

## Common Issues & Solutions

### Issue: Plugin not found
**Solution:** Check the plugin path in the docket. Make sure it's relative to the Host project's working directory.

### Issue: "No type implementing IKeryxFluxPlugin found"
**Solution:** Make sure the plugin class is `public` and has a parameterless constructor.

### Issue: Forwarding fails
**Solution:** Check the destination URL. Use https://webhook.site to get a test endpoint.

### Issue: DI errors
**Solution:** Make sure all services are registered in Program.cs before `builder.Build()`.

---

## Next Iteration Improvements

Once basic flow works:
1. Load dockets from YAML files (use YamlDotNet)
2. Dynamic receiver registration
3. Hot-reload support
4. Better error handling
5. OpenTelemetry integration

---

## Time Estimate

- Task 1 (PluginManager): 2-3 hours
- Task 2 (HttpReceiver): 1-2 hours
- Task 3 (HttpSender): 2-3 hours
- Task 4 (Sample Plugin): 1 hour
- Task 5 (Host wiring): 1-2 hours
- Task 6 (Testing/debugging): 2-3 hours

**Total: 1-2 days**

---

**Ready? Let's build the first vertical slice!** ??
