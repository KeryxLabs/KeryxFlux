using KeryxFlux.Application.Handlers;
using KeryxFlux.Application.Services;
using KeryxFlux.Application.FileSystem;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Infrastructure.Receivers;
using KeryxFlux.Infrastructure.Senders;
using KeryxFlux.Host.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProcessMessageCommandHandler).Assembly));

// Add HttpClient for senders
builder.Services.AddHttpClient();

// Register core services
builder.Services.AddSingleton<IPluginManager, PluginManager>();
builder.Services.AddSingleton<IDocketManager, DocketManager>();

// Register receivers and senders
builder.Services.AddSingleton<KeryxFlux.Domain.Ports.IReceiver, HttpReceiver>();
builder.Services.AddSingleton<KeryxFlux.Domain.Ports.ISender, HttpSender>();

// Register DocketMonitor
var docketsPath = Path.Combine(builder.Environment.ContentRootPath, "dockets");
builder.Services.AddSingleton<IDocketMonitor>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<DocketMonitor>>();
    var docketManager = sp.GetRequiredService<IDocketManager>();
    return new DocketMonitor(logger, docketManager, docketsPath);
});

// Register orchestration as hosted service
builder.Services.AddHostedService<DocketOrchestrationService>();

var app = builder.Build();

// Map all KeryxFlux endpoints
app.MapKeryxFluxEndpoints();

app.Run();







