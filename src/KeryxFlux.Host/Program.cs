using KeryxFlux.Application;
using KeryxFlux.Application.Handlers;
using KeryxFlux.Application.Services;
using KeryxFlux.Application.FileSystem;
using KeryxFlux.Application.Jobs;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Jobs;
using KeryxFlux.Domain.Ports;
using KeryxFlux.Infrastructure.Receivers;
using KeryxFlux.Infrastructure.Senders;
using KeryxFlux.Infrastructure.MessageBrokers.RabbitMq;
using KeryxFlux.Infrastructure.Factories;
using KeryxFlux.Host.Extensions;
using Hangfire;
using Hangfire.Redis.StackExchange;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add KeryxFlux Application layer (includes MediatR, PaginationOrchestrator, MultiTenantExpansionService)
builder.Services.AddKeryxFluxApplication();

// Add HttpClient for senders
builder.Services.AddHttpClient();

// Register core services
builder.Services.AddSingleton<IPluginManager, PluginManager>();
builder.Services.AddSingleton<IDocketManager, DocketManager>();

// Register RabbitMQ connection service
builder.Services.AddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();

// Register receiver and sender factories
builder.Services.AddSingleton<IReceiverFactory, ReceiverFactory>();
builder.Services.AddSingleton<ISenderFactory, SenderFactory>();

// Register individual receivers and senders (for legacy/direct use)
builder.Services.AddSingleton<IReceiver, HttpReceiver>();
builder.Services.AddSingleton<ISender, HttpSender>();

// Register polling jobs
builder.Services.AddScoped<IPollJob, TenantEndpointPollJob>();
builder.Services.AddScoped<TenantEndpointPollJob>();
builder.Services.AddSingleton<JobRegistrationService>();

// Configure Hangfire with Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseRedisStorage(
        ConnectionMultiplexer.Connect(redisConnectionString),
        new RedisStorageOptions
        {
            Prefix = "keryxflux:",
            ExpiryCheckInterval = TimeSpan.FromHours(1)
        }
    ));

// Add Hangfire server
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = builder.Configuration.GetValue<int>("Hangfire:WorkerCount", 10);
    options.Queues = builder.Configuration.GetSection("Hangfire:Queues").Get<string[]>() 
        ?? new[] { "default", "critical", "normal", "low" };
    options.ServerName = $"{Environment.MachineName}:keryxflux";
});

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

// Enable Hangfire Dashboard (IMPORTANT: Secure this in production!)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "KeryxFlux Job Dashboard"
});

// Map all KeryxFlux endpoints
app.MapKeryxFluxEndpoints();

app.Run();









