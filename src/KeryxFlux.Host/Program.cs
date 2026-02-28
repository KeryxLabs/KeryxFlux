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
using KeryxFlux.Infrastructure.MessageBrokers.Kafka;
using KeryxFlux.Infrastructure.MessageBrokers.Grpc;
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

// Register RabbitMQ services
builder.Services.AddSingleton<IRabbitMqConnectionService, RabbitMqConnectionService>();
builder.Services.AddSingleton<IRabbitMqReceiverService, RabbitMqReceiver>();

// Register Kafka services
builder.Services.AddSingleton<IKafkaConsumerService, KafkaConsumer>();

// Register gRPC services
builder.Services.AddSingleton<IGrpcReceiverService, GrpcReceiver>();

// Register senders - looked up by their Type property
builder.Services.AddSingleton<ISender, HttpSender>();
builder.Services.AddSingleton<ISender, RabbitMqSender>();
builder.Services.AddSingleton<ISender, KafkaProducer>();
builder.Services.AddSingleton<ISender, GrpcSender>();

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

// Log registered services at startup
using (var scope = app.Services.CreateScope())
{
    var senders = scope.ServiceProvider.GetServices<ISender>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation(
        "Registered senders: {SenderTypes}",
        string.Join(", ", senders.Select(s => s.Type)));
    
    logger.LogInformation(
        "RabbitMQ receiver service registered for dynamic consumer management");
}

// Enable Hangfire Dashboard (IMPORTANT: Secure this in production!)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "KeryxFlux Job Dashboard"
});

// Map all KeryxFlux endpoints
app.MapKeryxFluxEndpoints();

app.Run();









