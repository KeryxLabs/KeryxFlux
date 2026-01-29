# KeryxFlux - Bi-Directional Interoperability Engine
## Architecture & Implementation Plan

> **Status:** Planning Phase  
> **Version:** 1.0  
> **Last Updated:** 2024  
> **Target Framework:** .NET 10  
> **Brand:** KeryxHealth

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Project Migration & Naming](#project-migration--naming)
3. [Deployment Architecture](#deployment-architecture)
4. [Service Architecture](#service-architecture)
5. [HTTP Receiver Design](#http-receiver-design)
6. [Project Structure](#project-structure)
7. [Configuration & Docket Management](#configuration--docket-management)
8. [Hangfire Integration](#hangfire-integration)
9. [Scalability & High Availability](#scalability--high-availability)
10. [Observability Strategy](#observability-strategy)
11. [Security Considerations](#security-considerations)
12. [Development Workflow](#development-workflow)
13. [Migration Path](#migration-path)
14. [Open Questions & Decisions](#open-questions--decisions)
15. [Success Criteria](#success-criteria)
16. [Risk Assessment](#risk-assessment)

---

## Executive Summary

**KeryxFlux** is a cloud-native, horizontally scalable **bi-directional** interoperability engine deployable as a single Docker image that can operate in two modes:
- **Main Mode**: Coordinates scheduling, manages docket lifecycle, hosts management APIs
- **Node Mode**: Executes jobs, processes messages, handles transformations

### Brand Alignment

Part of the **KeryxHealth** ecosystem:
- **KeryxPars**: Interface message parsing library (Greek "Herald" + Latin "Parts")
- **KeryxFlux**: Interoperability flow engine (Greek "Herald" + Latin "Flow")

### Key Differentiators

Unlike traditional interoperability engines (Mirth Connect, Rhapsody):
- ? **DevOps-First**: YAML-based declarative configuration
- ? **Bi-Directional**: Both send (poller) and receive (listener) capabilities
- ? **Cloud-Native**: Container-ready with horizontal scaling
- ? **Plugin System**: Hot-loadable transformation plugins
- ? **Modern Stack**: .NET 10, Minimal APIs, Hangfire
- ? **Multi-Protocol**: HTTP, TCP, RabbitMQ, Kafka support

---

## Project Migration & Naming

### Current State
- **Current Repo:** `https://github.com/theelevators/ReqStr`
- **Current Project:** `ReqStr.Core` (unidirectional request/response library)

### Migration Strategy

#### Step 1: Branding Decision

**Selected Name:** **`KeryxFlux`**

**Rationale:**
- **Keryx** (Greek: ?????): Herald, messenger, announcer
- **Flux** (Latin): Flow, continuous change, movement
- **Combined**: "Herald of Flow" - perfectly describes an interoperability engine

**Brand Consistency:**
- Aligns with existing **KeryxPars** library (parsing/parts)
- Establishes **KeryxHealth** as a recognizable brand
- Healthcare industry will associate Keryx* products as interoperability solutions

**Technical Naming:**
- `.NET` friendly: `KeryxFlux.Core`, `KeryxFlux.Host`
- Docker friendly: `keryxflux:latest`
- Namespace friendly: `namespace KeryxFlux.Domain`
- URL friendly: `keryxflux.io` or `keryxhealth.io`

#### Step 2: Create New Repository

```bash
# 1. Create new GitHub repo (via GitHub UI)
#    Name: KeryxFlux
#    Description: "Cloud-native bi-directional interoperability engine for healthcare and enterprise integration"
#    License: Apache 2.0

# 2. Clone new empty repo
git clone https://github.com/theelevators/KeryxFlux.git
cd KeryxFlux

# 3. Initialize with base files
echo "# KeryxFlux" > README.md
echo "Part of the KeryxHealth ecosystem" >> README.md
echo "*.swp\n*.user\nbin/\nobj/" > .gitignore
git add .
git commit -m "Initial commit: KeryxFlux interoperability engine"
git push origin main
```

#### Step 3: Migrate Useful Code from ReqStr

**What to Migrate:**

? **Keep & Refactor:**
- `Models/` (Docket, Endpoint, Step, ServerInformation)
- `Abstractions/` (Result pattern, IReqStrAdapter concept)
- `Utils/Loading/` (Loader, LibraryLoadContext)
- `Requesting/HttpReqStr.cs` (refactor to HttpSenderService)

? **Leave Behind:**
- Current project structure (not DDD/Hexagonal)
- Existing namespace `ReqStr`
- Build artifacts in `obj/bin`

**Migration Commands:**

```bash
# In ReqStr repo
cd D:\Development\Csharp\ReqStr

# Create archive of useful files
git archive --format=zip --output=../reqstr-migration.zip HEAD ReqStr.Core/Models ReqStr.Core/Abstractions ReqStr.Core/Utils

# In new KeryxFlux repo
cd D:\Development\Csharp\KeryxFlux
unzip ../reqstr-migration.zip -d migration-temp/

# Manually reorganize into new structure
# Models ? KeryxFlux.Domain/Models
# Abstractions ? KeryxFlux.Domain/Ports
# Utils ? KeryxFlux.Infrastructure/Plugins
```

#### Step 4: Update Namespaces

```csharp
// Old (ReqStr)
namespace ReqStr.Core.Models

// New (KeryxFlux)
namespace KeryxFlux.Domain.Models
```

**Find/Replace Strategy:**
```bash
# Use VS Code or Rider find/replace across solution
Find:    namespace ReqStr
Replace: namespace KeryxFlux

Find:    using ReqStr
Replace: using KeryxFlux
```

#### Step 5: Update Project Files

**Old:** `ReqStr.Core.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

**New:** `KeryxFlux.Domain.csproj`
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>KeryxFlux.Domain</RootNamespace>
    <Company>KeryxHealth</Company>
    <Product>KeryxFlux</Product>
    <Description>Domain models for KeryxFlux interoperability engine</Description>
  </PropertyGroup>
</Project>
```

#### Step 6: Create Migration Document

Create `MIGRATION.md` in new repo documenting:
- What was migrated from ReqStr
- What was rewritten
- Breaking changes
- Namespace mapping

---

## Deployment Architecture

### Single Image, Multiple Modes

```
???????????????????????????????????????????????????????????????
?                  KeryxFlux:latest                           ?
?                  (Single Docker Image)                       ?
???????????????????????????????????????????????????????????????
                            ?
                            ???????????????????????????????????
                            ?                                 ?
                    ??????????????????              ????????????????????
                    ?  MAIN MODE     ?              ?   NODE MODE      ?
                    ??????????????????              ????????????????????
                    ? • Hangfire     ?              ? • Hangfire       ?
                    ?   Dashboard    ?              ?   Workers        ?
                    ? • Job Scheduler?              ? • Job Processors ?
                    ? • Docket       ?              ? • HTTP Receivers ?
                    ?   Monitor      ?              ? • TCP Receivers  ?
                    ? • Management   ?              ? • Queue          ?
                    ?   API          ?              ?   Consumers      ?
                    ? • Metrics      ?              ? • Transformers   ?
                    ?   Collector    ?              ? • Senders        ?
                    ??????????????????              ????????????????????
                         1 instance                    N instances
```

### Mode Selection Strategy

**Environment Variable:**
```yaml
KERYXFLUX_MODE: "main"  # or "node"
```

**Behavior Matrix:**

| Component | Main Mode | Node Mode |
|-----------|-----------|-----------|
| Hangfire Server | ? Enabled (creates jobs) | ? Disabled |
| Hangfire Worker | ? Enabled (processes jobs) | ? Enabled (processes jobs) |
| Docket Monitor | ? Enabled (watches file changes) | ? Disabled |
| HTTP Receivers | ? Enabled (routes to workers) | ? Enabled (processes requests) |
| TCP Receivers | ? Disabled | ? Enabled (1 node only via leader election) |
| Queue Consumers | ? Disabled | ? Enabled (all nodes consume) |
| Management API | ? Enabled | ? Disabled |
| Health Checks | ? Full | ? Worker-only |

### Docker Compose Example

```yaml
version: '3.8'

services:
keryxflux-main:
  image: keryxflux:latest
  container_name: keryxflux-main
  environment:
    - KERYXFLUX_MODE=main
    - ASPNETCORE_URLS=http://+:8080
    - ConnectionStrings__Hangfire=Server=sqlserver;Database=KeryxFlux_Hangfire;User=sa;Password=YourStrong!Pass;
    - ConnectionStrings__Redis=redis:6379
  ports:
    - "8080:8080"  # Management API + Hangfire Dashboard
  volumes:
    - ./dockets:/app/dockets:ro
    - ./plugins:/app/plugins:ro
  depends_on:
    - sqlserver
    - redis
    - rabbitmq
  restart: unless-stopped
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
    interval: 30s
    timeout: 10s
    retries: 3
      
keryxflux-node:
  image: keryxflux:latest
  environment:
    - KERYXFLUX_MODE=node
      - ASPNETCORE_URLS=http://+:8080
      - ConnectionStrings__Hangfire=Server=sqlserver;Database=KeryxFlux_Hangfire;User=sa;Password=YourStrong!Pass;
      - ConnectionStrings__Redis=redis:6379
    ports:
      - "8081-8085:8080"  # Dynamic ports for HTTP receivers
      - "6661-6665:6661"  # Dynamic ports for TCP receivers (MLLP)
    volumes:
      - ./dockets:/app/dockets:ro
      - ./plugins:/app/plugins:ro
    depends_on:
      - keryxflux-main
      - redis
      - rabbitmq
    restart: unless-stopped
    deploy:
      replicas: 5  # Horizontal scaling
      
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong!Pass
      - MSSQL_PID=Developer
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    restart: unless-stopped
    
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    restart: unless-stopped
    
  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      - RABBITMQ_DEFAULT_USER=admin
      - RABBITMQ_DEFAULT_PASS=admin123
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq
    restart: unless-stopped

volumes:
  sqlserver-data:
  redis-data:
  rabbitmq-data:
```

---

## Service Architecture (DI Strategy)

### Service Lifetime Design

All communication adapters as **Singletons** with internal connection pooling:

```csharp
// Service registration (conceptual - not actual implementation yet)
public static class ServiceConfiguration
{
    public static IServiceCollection AddKeryxFluxServices(this IServiceCollection services, IConfiguration config)
    {
        // Core services
        services.AddSingleton<IReceiverRegistry, ReceiverRegistry>();
        services.AddSingleton<ISenderRegistry, SenderRegistry>();
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddSingleton<IDocketManager, DocketManager>();
        services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();
        
        // Receiver singletons (created dynamically based on docket config)
        services.AddSingleton<HttpReceiverService>();
        services.AddSingleton<TcpReceiverService>();
        services.AddSingleton<RabbitMQReceiverService>();
        services.AddSingleton<KafkaReceiverService>();
        
        // Sender singletons
        services.AddSingleton<HttpSenderService>();
        services.AddSingleton<RabbitMQSenderService>();
        services.AddSingleton<KafkaSenderService>();
        services.AddSingleton<TcpSenderService>();
        
        return services;
    }
}
```

**Rationale:**
- **Singletons** maintain connection pools (HttpClient, RabbitMQ channels, Kafka producers)
- Avoid connection overhead on every request
- Thread-safe implementations required
- Supports high throughput (>1000 req/sec per node)

### Dynamic Receiver Registration

**Workflow:**
1. `DocketManager` reads YAML from `/dockets` directory
2. If `type: receiver`, registers appropriate receiver service
3. Service stores routing information in `IReceiverRegistry`
4. HTTP catch-all endpoint queries registry for routing
5. On docket removal, service unregisters from registry

---

## HTTP Receiver Design (Minimal API Catch-All)

### Single Endpoint Strategy

**Flow Diagram:**
```
Incoming Request:
POST /receive/hl7-inbound HTTP/1.1
Content-Type: application/json
X-API-Key: secret123

{
  "patient_id": "12345",
  "admission_date": "2024-01-15"
}

?
?? 1. Catch-all endpoint receives request
?
?? 2. Extract path: "/receive/hl7-inbound"
?
?? 3. ReceiverRegistry.TryGetDocketByEndpoint(path, out docket)
?     ?? Not found? ? 404 Not Found
?     ?? Found? ? Continue
?
?? 4. Validate authentication (if configured in docket)
?     ?? API Key validation
?     ?? Bearer token validation
?     ?? OAuth2 validation
?
?? 5. Rate limiting check (Redis)
?     ?? Exceeded? ? 429 Too Many Requests
?
?? 6. Pass to WorkflowOrchestrator.ProcessInboundAsync(docket, payload)
?     ?? Get plugin instance
?     ?? Transform data
?     ?? Forward to destinations
?
?? 7. Return response
      ?? 202 Accepted (async processing)
      ?? 200 OK (sync processing with result)
```

### Receiver Registry Design

```csharp
// Conceptual interface (not implementation)
public interface IReceiverRegistry
{
    // HTTP endpoint registration
    bool TryGetDocketByEndpoint(string path, out DocketConfig docket);
    
    // RabbitMQ queue registration
    bool TryGetDocketByQueueName(string queueName, out DocketConfig docket);
    
    // Kafka topic registration
    bool TryGetDocketByTopic(string topic, out DocketConfig docket);
    
    // TCP port registration
    bool TryGetDocketByPort(int port, out DocketConfig docket);
    
    // Management
    void RegisterReceiver(DocketConfig docket);
    void UnregisterReceiver(string docketName);
    IEnumerable<DocketConfig> GetAllReceivers();
}

// In-memory data structure (conceptual)
internal class ReceiverRegistry : IReceiverRegistry
{
    private readonly ConcurrentDictionary<string, DocketConfig> _httpEndpoints;   // "/receive/hl7" -> docket
    private readonly ConcurrentDictionary<string, DocketConfig> _rabbitQueues;    // "orders-queue" -> docket
    private readonly ConcurrentDictionary<string, DocketConfig> _kafkaTopics;     // "events-topic" -> docket
    private readonly ConcurrentDictionary<int, DocketConfig> _tcpPorts;           // 6661 -> docket
}
```

### Minimal API Implementation Pattern

```csharp
// Conceptual endpoint registration (not actual code)
app.MapPost("/receive/{**path}", async (
    string path,
    HttpContext context,
    IReceiverRegistry registry,
    IWorkflowOrchestrator orchestrator) =>
{
    // 1. Lookup docket
    if (!registry.TryGetDocketByEndpoint($"/receive/{path}", out var docket))
    {
        return Results.NotFound(new { error = "No receiver configured for this endpoint" });
    }
    
    // 2. Validate authentication
    if (!await ValidateAuthenticationAsync(context, docket))
    {
        return Results.Unauthorized();
    }
    
    // 3. Read body
    using var reader = new StreamReader(context.Request.Body);
    var body = await reader.ReadToEndAsync();
    var bytes = Encoding.UTF8.GetBytes(body);
    
    // 4. Process workflow
    var result = await orchestrator.ProcessInboundAsync(docket.Name, bytes);
    
    // 5. Return result
    return result.IsSuccess 
        ? Results.Accepted(new { message = "Processing started", id = result.Value })
        : Results.BadRequest(new { error = result.Error });
});
```

### Authentication Validation

```csharp
// Conceptual helper (not implementation)
private async Task<bool> ValidateAuthenticationAsync(HttpContext context, DocketConfig docket)
{
    return docket.Receiver.Authentication.Type switch
    {
        AuthenticationType.None => true,
        
        AuthenticationType.ApiKey => 
            context.Request.Headers.TryGetValue(docket.Receiver.Authentication.Header, out var key) &&
            key == Environment.GetEnvironmentVariable(docket.Receiver.Authentication.SecretEnv),
        
        AuthenticationType.Bearer =>
            context.Request.Headers.TryGetValue("Authorization", out var bearer) &&
            bearer.ToString().StartsWith("Bearer ") &&
            await ValidateBearerTokenAsync(bearer.ToString()[7..], docket),
        
        AuthenticationType.OAuth2 =>
            await ValidateOAuth2TokenAsync(context, docket),
        
        _ => false
    };
}
```

---

## Project Structure (DDD + Hexagonal)

```
KeryxFlux/
?
??? src/
?   ?
?   ??? KeryxFlux.Domain/                    # Pure domain logic (no external deps)
?   ?   ??? Models/
?   ?   ?   ??? Docket.cs
?   ?   ?   ??? Step.cs
?   ?   ?   ??? WorkflowExecution.cs
?   ?   ?   ??? TransformedData.cs
?   ?   ?   ??? ReceiverConfig.cs
?   ?   ??? ValueObjects/
?   ?   ?   ??? LibraryPath.cs
?   ?   ?   ??? EndpointPath.cs
?   ?   ?   ??? DocketName.cs
?   ?   ?   ??? DocketVersion.cs
?   ?   ??? Ports/                            # Hexagonal "ports" (interfaces)
?   ?   ?   ??? ITransformationPort.cs        # Plugin contract
?   ?   ?   ??? IReceivingPort.cs             # Inbound adapter contract
?   ?   ?   ??? ISendingPort.cs               # Outbound adapter contract
?   ?   ?   ??? IStoragePort.cs               # State persistence contract
?   ?   ??? Enums/
?   ?   ?   ??? DocketType.cs                 # Poller vs Receiver
?   ?   ?   ??? CommunicationType.cs
?   ?   ?   ??? AuthenticationType.cs
?   ?   ??? Exceptions/
?   ?       ??? DomainException.cs
?   ?       ??? DocketValidationException.cs
?   ?
?   ??? KeryxFlux.Application/               # Use cases & orchestration
?   ?   ??? UseCases/
?   ?   ?   ??? ExecutePollerDocketUseCase.cs
?   ?   ?   ??? ProcessReceivedMessageUseCase.cs
?   ?   ?   ??? TransformAndForwardUseCase.cs
?   ?   ?   ??? LoadDocketUseCase.cs
?   ?   ??? Services/
?   ?   ?   ??? DocketManager.cs
?   ?   ?   ??? PluginManager.cs
?   ?   ?   ??? WorkflowOrchestrator.cs
?   ?   ?   ??? ReceiverRegistry.cs
?   ?   ??? DTOs/
?   ?   ?   ??? WorkflowRequest.cs
?   ?   ?   ??? WorkflowResponse.cs
?   ?   ?   ??? DocketSummary.cs
?   ?   ??? Interfaces/
?   ?       ??? IDocketManager.cs
?   ?       ??? IPluginManager.cs
?   ?       ??? IWorkflowOrchestrator.cs
?   ?       ??? IReceiverRegistry.cs
?   ?
?   ??? KeryxFlux.Infrastructure/            # Adapters (implementations)
?   ?   ??? Plugins/
?   ?   ?   ??? PluginLoader.cs               # Assembly loading
?   ?   ?   ??? PluginIsolationContext.cs
?   ?   ??? Receivers/                        # Inbound adapters
?   ?   ?   ??? HttpReceiverService.cs
?   ?   ?   ??? TcpReceiverService.cs
?   ?   ?   ??? RabbitMQReceiverService.cs
?   ?   ?   ??? KafkaReceiverService.cs
?   ?   ??? Senders/                          # Outbound adapters
?   ?   ?   ??? HttpSenderService.cs
?   ?   ?   ??? RabbitMQSenderService.cs
?   ?   ?   ??? KafkaSenderService.cs
?   ?   ?   ??? TcpSenderService.cs
?   ?   ??? Persistence/
?   ?   ?   ??? RedisCacheAdapter.cs
?   ?   ?   ??? SqlServerStateStore.cs
?   ?   ?   ??? InMemoryCacheAdapter.cs
?   ?   ??? Scheduling/
?   ?   ?   ??? HangfireJobScheduler.cs
?   ?   ??? FileSystem/
?   ?   ?   ??? DocketFileWatcher.cs
?   ?   ?   ??? YamlDocketLoader.cs
?   ?   ??? Telemetry/
?   ?       ??? OpenTelemetryProvider.cs
?   ?       ??? PrometheusMetricsExporter.cs
?   ?
?   ??? KeryxFlux.Host/                      # Unified host application
?   ?   ??? Program.cs                        # Main entry point
?   ?   ??? HostModes/
?   ?   ?   ??? MainModeConfigurator.cs
?   ?   ?   ??? NodeModeConfigurator.cs
?   ?   ??? Endpoints/
?   ?   ?   ??? ReceiverEndpoints.cs          # Minimal API catch-all
?   ?   ?   ??? ManagementEndpoints.cs        # Docket/Plugin CRUD
?   ?   ?   ??? HealthEndpoints.cs            # /health, /ready
?   ?   ??? Middleware/
?   ?   ?   ??? RequestLoggingMiddleware.cs
?   ?   ?   ??? ErrorHandlingMiddleware.cs
?   ?   ?   ??? MetricsMiddleware.cs
?   ?   ??? Extensions/
?   ?   ?   ??? ServiceCollectionExtensions.cs
?   ?   ??? appsettings.json
?   ?   ??? appsettings.Development.json
?   ?   ??? Dockerfile
?   ?
?   ??? KeryxFlux.Contracts/                 # Shared contracts for plugins
?       ??? IKeryxFluxPlugin.cs              # What plugin developers implement
?       ??? TransformationContext.cs
?       ??? PluginMetadata.cs
?
??? plugins/                                   # Sample plugins
?   ??? KeryxFlux.Plugins.HL7Parser/
?   ??? KeryxFlux.Plugins.FhirTransformer/
?   ??? KeryxFlux.Plugins.JsonMapper/
?
??? tests/
?   ??? KeryxFlux.Domain.Tests/
?   ??? KeryxFlux.Application.Tests/
?   ??? KeryxFlux.Infrastructure.Tests/
?   ??? KeryxFlux.E2E.Tests/
?
??? docs/
?   ??? ARCHITECTURE.md                       # This file
?   ??? MIGRATION.md                          # ReqStr migration guide
?   ??? PLUGIN_DEVELOPMENT.md
?   ??? DEPLOYMENT.md
?
??? dockets/                                   # Example docket configurations
?   ??? examples/
?   ?   ??? http-receiver.yaml
?   ?   ??? poller.yaml
?   ?   ??? rabbitmq-consumer.yaml
?   ??? README.md
?
??? .github/
?   ??? workflows/
?       ??? ci.yml
?       ??? release.yml
?
??? docker-compose.yml
??? docker-compose.dev.yml
??? .gitignore
??? .dockerignore
??? LICENSE
??? README.md
```

---

## Configuration & Docket Management

### Docket Synchronization Strategy

**Challenge:** Nodes need to know about dockets loaded by Main

**Solution: Shared Volume + Redis Pub/Sub**

```
???????????????????????????????????????????????????????????????
?                      Docket Lifecycle                        ?
???????????????????????????????????????????????????????????????

1. Main Mode:
   ?? FileSystemWatcher monitors /dockets directory
   ?? On file change (Created/Modified/Deleted):
   ?  ?? Load YAML via YamlDocketLoader
   ?  ?? Validate schema
   ?  ?? Update DocketManager in-memory
   ?  ?? Publish "docket.loaded" event to Redis channel
   ?  ?? Store docket JSON in Redis: "dockets:{name}"
   ?
   ?? On startup:
      ?? Load all existing dockets from /dockets

2. Node Mode:
   ?? On startup:
   ?  ?? Pull all dockets from Redis ("dockets:*")
   ?
   ?? Runtime:
      ?? Subscribe to Redis channel: "docket.events"
      ?? On "docket.loaded" event: Pull from Redis and register
      ?? On "docket.unloaded" event: Unregister
      ?? On "docket.updated" event: Reload configuration
```

### Enhanced YAML Schema

#### Receiver Docket (HTTP)

```yaml
# /dockets/hl7-receiver.yaml
name: hl7-admission-receiver
version: 1.0.0
type: receiver                              # receiver | poller
plugin_location: ./plugins/HL7Parser.dll

# Receiver configuration
receiver:
  type: http                                # http | tcp | rabbitmq | kafka
  endpoint: /receive/hl7-admissions         # HTTP path (for type: http)
  
  authentication:
    type: api_key                           # none | api_key | bearer | oauth2
    header: X-API-Key
    secret_env: HL7_API_KEY                 # Environment variable name
  
  rate_limiting:
    enabled: true
    requests_per_minute: 100
    burst_size: 20
  
  processing:
    mode: async                             # async | sync
    timeout_seconds: 30
    max_concurrent: 10

# Forwarding destinations
forwarding:
  destinations:
    - name: primary-ehr
      type: http
      url: https://ehr.example.com/api/admissions
      method: POST
      
      retry_policy:
        max_attempts: 3
        backoff_strategy: exponential       # exponential | linear | fixed
        initial_delay_seconds: 1
        max_delay_seconds: 60
      
      headers:
        Content-Type: application/json
        Authorization: Bearer ${EHR_TOKEN}
      
      timeout_seconds: 30
        
    - name: audit-stream
      type: kafka
      bootstrap_servers: kafka:9092
      topic: hl7-audit-log
      partition_key: patient_id             # Field from transformed data
      
# Telemetry
telemetry:
  trace_requests: true
  log_level: Information                    # Trace | Debug | Information | Warning | Error
  metrics:
    - request_count
    - processing_duration
    - transformation_errors
    - forwarding_attempts
```

#### Receiver Docket (TCP/MLLP)

```yaml
# /dockets/mllp-receiver.yaml
name: mllp-receiver
version: 1.0.0
type: receiver
plugin_location: ./plugins/HL7Parser.dll

receiver:
  type: tcp
  port: 6661
  protocol: mllp                            # mllp | raw
  encoding: UTF-8
  
  connection:
    max_connections: 50
    timeout_seconds: 60
    keep_alive: true
    buffer_size: 8192
  
  authentication:
    type: none                              # TCP typically no auth
  
  processing:
    mode: sync                              # Return ACK/NACK immediately
    
forwarding:
  destinations:
    - name: ehr-system
      type: http
      url: https://ehr.example.com/api/hl7
      method: POST
```

#### Receiver Docket (RabbitMQ Consumer)

```yaml
# /dockets/order-consumer.yaml
name: order-processor
version: 1.0.0
type: receiver
plugin_location: ./plugins/OrderTransformer.dll

receiver:
  type: rabbitmq
  connection_env: RABBITMQ_CONNECTION       # amqp://user:pass@host:5672
  queue: incoming-orders
  
  consumer:
    prefetch_count: 10                      # Number of messages to prefetch
    auto_ack: false                         # Manual acknowledgment
    exclusive: false
    
  processing:
    mode: async
    retry_on_failure: true
    max_retries: 3
    dead_letter_exchange: orders-dlx
    
forwarding:
  destinations:
    - name: erp-system
      type: http
      url: https://erp.example.com/api/orders
      method: POST
```

#### Receiver Docket (Kafka Consumer)

```yaml
# /dockets/event-processor.yaml
name: event-stream-processor
version: 1.0.0
type: receiver
plugin_location: ./plugins/EventParser.dll

receiver:
  type: kafka
  bootstrap_servers: kafka:9092
  topic: user-events
  
  consumer:
    group_id: KeryxFlux-processors
    auto_offset_reset: earliest             # earliest | latest
    enable_auto_commit: false               # Manual commit after successful processing
    
  processing:
    mode: async
    max_concurrent: 5
    
forwarding:
  destinations:
    - name: processed-events
      type: kafka
      bootstrap_servers: kafka:9092
      topic: processed-user-events
```

#### Poller Docket (Scheduled HTTP Requests)

```yaml
# /dockets/patient-sync.yaml
name: patient-sync-poller
version: 1.0.0
type: poller
plugin_location: ./plugins/FhirParser.dll

# Scheduling
scheduler:
  cron_expression: "*/15 * * * *"           # Every 15 minutes
  timezone: America/New_York
  queue: default                            # Hangfire queue name (default | critical | low)
  timeout_seconds: 300
  enabled: true

# Source server
server_information:
  name: epic-fhir
  address: https://fhir.epic.com
  port: 443
  communication_type: http
  timeout_seconds: 30
  
  authentication:
    type: oauth2
    token_url: https://oauth.epic.com/token
    client_id_env: EPIC_CLIENT_ID
    client_secret_env: EPIC_CLIENT_SECRET
    scope: "patient/*.read"
    
  retry_policy:
    max_attempts: 3
    backoff_strategy: exponential

# Endpoints to poll
endpoints:
  - name: recent-patients
    path: /Patient?_lastUpdated=gt{last_run_time}
    max_attempts: 3
    is_cached: true
    cached_property: last_run_time
    template_arguments:
      - name: last_run_time
        format: "yyyy-MM-ddTHH:mm:ssZ"
        
# Forwarding
forwarding:
  destinations:
    - name: patient-queue
      type: rabbitmq
      connection_env: RABBITMQ_CONNECTION
      exchange: patient-updates
      routing_key: patient.created
      persistent: true
      
    - name: backup-storage
      type: http
      url: https://backup.example.com/api/patients
      method: POST
```

### Docket Validation Rules

```csharp
// Conceptual validation rules (not implementation)
public class DocketValidator
{
    public Result<Docket> Validate(string yaml)
    {
        // 1. YAML syntax validation
        // 2. Required fields present
        // 3. Type-specific validation:
        //    - receiver: must have receiver config
        //    - poller: must have scheduler + server_information
        // 4. Plugin file exists
        // 5. Endpoint paths unique across all dockets
        // 6. Queue/topic names valid
        // 7. Environment variables referenced exist (warning)
        // 8. Cron expression valid
        // 9. URLs well-formed
        // 10. Circular dependencies check (if dockets reference each other)
    }
}
```

---

## Hangfire Integration

### Job Distribution Model

```
????????????????
?  Main Mode   ?
?  (1 instance)?
?              ?
? • Creates    ?
?   Recurring  ?
?   Jobs       ?
????????????????
       ?
       ? INSERT INTO HangfireJobs
       ?
???????????????????????
?  Hangfire Storage   ?
?   (SQL Server)      ?
?                     ?
? • Jobs table        ?
? • State table       ?
? • Queue table       ?
???????????????????????
       ?
       ? SELECT * FROM Queue
       ?
????????????????????????????????????????
?         Hangfire Workers             ?
?  ????????????  ????????????         ?
?  ? Main     ?  ? Node 1   ?  ...    ?
?  ? (Worker) ?  ? (Worker) ?         ?
?  ????????????  ????????????         ?
?                                      ?
? • Poll for jobs                      ?
? • Execute jobs                       ?
? • Update job state                   ?
????????????????????????????????????????
```

### Hangfire Configuration

```csharp
// Conceptual Hangfire setup (not implementation)
public static class HangfireConfiguration
{
    public static IServiceCollection AddKeryxFluxHangfire(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isMainMode)
    {
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                configuration.GetConnectionString("Hangfire"),
                new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.FromSeconds(1),
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));
        
        // Add server (job creator) only in Main mode
        if (isMainMode)
        {
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = 0;  // Main doesn't process jobs
                options.ServerName = "KeryxFlux-Main";
            });
        }
        else
        {
            // Add worker (job processor) in Node mode
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = Environment.ProcessorCount * 2;
                options.Queues = new[] { "critical", "default", "low" };
                options.ServerName = $"KeryxFlux-Node-{Environment.MachineName}";
            });
        }
        
        return services;
    }
}
```

### Queue Strategy

**Priority Queues:**

| Queue | Purpose | Worker Priority | Examples |
|-------|---------|-----------------|----------|
| `critical` | Real-time, low latency | Highest | Patient alerts, urgent orders |
| `default` | Standard workflows | Normal | Regular data sync, transformations |
| `low` | Batch jobs, reporting | Lowest | Nightly aggregations, cleanup |

**Queue Assignment in Docket:**
```yaml
scheduler:
  cron_expression: "*/5 * * * *"
  queue: critical  # Routes to 'critical' Hangfire queue
```

### Job Execution Flow

```csharp
// Conceptual job execution (not implementation)

// 1. Job registration (Main mode on docket load)
RecurringJob.AddOrUpdate<PollerExecutor>(
    recurringJobId: docket.Name,
    methodCall: x => x.ExecutePollerDocket(docket.Name),
    cronExpression: docket.Scheduler.CronExpression,
    options: new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.FindSystemTimeZoneById(docket.Scheduler.Timezone),
        Queue = docket.Scheduler.Queue
    });

// 2. Job execution (Any worker node)
public class PollerExecutor
{
    private readonly IWorkflowOrchestrator _orchestrator;
    
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecutePollerDocket(string docketName)
    {
        await _orchestrator.ExecutePollerAsync(docketName, CancellationToken.None);
    }
}

// 3. Orchestrator workflow
public async Task ExecutePollerAsync(string docketName, CancellationToken ct)
{
    // a. Get docket
    var docket = await _docketManager.GetDocketAsync(docketName);
    
    // b. Get plugin
    var plugin = await _pluginManager.GetPluginInstanceAsync(docket.PluginLocation);
    
    // c. Execute HTTP requests
    var httpClient = _httpClientFactory.CreateClient();
    var response = await httpClient.GetAsync(docket.GenerateRequestUrl());
    var data = await response.Content.ReadAsByteArrayAsync();
    
    // d. Transform
    var transformResult = plugin.Transform(data, context);
    
    // e. Forward
    foreach (var destination in docket.Forwarding.Destinations)
    {
        await _senderRegistry.SendAsync(destination, transformResult.Data);
    }
}
```

### Hangfire Dashboard

**Access:**
- URL: `http://main-instance:8080/hangfire`
- Authentication: Configure in `Program.cs`

**Features:**
- View recurring jobs
- Monitor job execution
- Retry failed jobs manually
- View server statistics
- Manage queues

```csharp
// Conceptual dashboard configuration
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    StatsPollingInterval = 5000,
    DisplayStorageConnectionString = false
});
```

---

## Redis Architecture & Configuration

### Why Redis for Both Caching AND Hangfire

**Decision**: Use Redis as the single data store for both distributed caching and Hangfire job storage.

**Rationale:**
1. **Simplified Infrastructure**: One dependency instead of two (no SQL Server needed)
2. **Better Horizontal Scaling**: Redis Cluster scales linearly
3. **Lower Latency**: In-memory job pickup (10-100x faster than SQL queries)
4. **Cost Effective**: Single Redis instance/cluster vs SQL Server + Redis
5. **Cloud-Native**: Easier deployment in containers and Kubernetes
6. **Consistency**: Same technology for all state management

### Redis Data Structure Usage

```
Redis Key Space Organization:
??? keryxflux:dockets:{name}           # Docket configurations (Hash)
??? keryxflux:cache:processing:{name}  # Processing time cache (String with TTL)
??? keryxflux:jobs:*                   # Hangfire jobs (Sorted Set)
??? keryxflux:queue:critical           # Hangfire critical queue (List)
??? keryxflux:queue:default            # Hangfire default queue (List)
??? keryxflux:queue:low                # Hangfire low priority queue (List)
??? keryxflux:pubsub:dockets           # Pub/Sub for docket changes (Channel)
```

### Redis Persistence Configuration

**Critical for Production**: Enable AOF (Append-Only File) persistence

```yaml
# docker-compose.yml
redis:
  image: redis:7-alpine
  command: >
    redis-server
    --appendonly yes
    --appendfsync everysec
    --maxmemory 2gb
    --maxmemory-policy allkeys-lru
  ports:
    - \"6379:6379\"
  volumes:
    - redis-data:/data
  restart: unless-stopped
  healthcheck:
    test: [\"CMD\", \"redis-cli\", \"ping\"]
    interval: 10s
    timeout: 3s
    retries: 3
```

**Configuration Explained:**
- `--appendonly yes`: Enable AOF persistence (durability)
- `--appendfsync everysec`: Fsync every second (balance between performance and durability)
- `--maxmemory 2gb`: Limit memory usage per instance
- `--maxmemory-policy allkeys-lru`: Evict least recently used keys when memory is full

### Redis High Availability Options

#### Option 1: Redis Sentinel (Recommended for Production)

```yaml
# 3 node setup: 1 master + 2 replicas with Sentinel
version: '3.8'
services:
  redis-master:
    image: redis:7-alpine
    command: redis-server --appendonly yes --appendfsync everysec
    volumes:
      - redis-master-data:/data
      
  redis-replica-1:
    image: redis:7-alpine
    command: redis-server --appendonly yes --replicaof redis-master 6379
    volumes:
      - redis-replica1-data:/data
      
  redis-replica-2:
    image: redis:7-alpine
    command: redis-server --appendonly yes --replicaof redis-master 6379
    volumes:
      - redis-replica2-data:/data
      
  redis-sentinel-1:
    image: redis:7-alpine
    command: redis-sentinel /etc/redis/sentinel.conf
    
  redis-sentinel-2:
    image: redis:7-alpine
    command: redis-sentinel /etc/redis/sentinel.conf
    
  redis-sentinel-3:
    image: redis:7-alpine
    command: redis-sentinel /etc/redis/sentinel.conf
```

#### Option 2: Redis Cluster (For Very Large Scale)

Use when:
- Need to scale beyond single master capacity
- Have >10GB of data
- Running 10+ KeryxFlux nodes

```bash
# Redis Cluster requires minimum 6 nodes (3 masters + 3 replicas)
# Typically deployed via Kubernetes Operator or Managed Service (Azure Cache, AWS ElastiCache)
```

#### Option 3: Managed Redis (Easiest for Production)

**Cloud Providers:**
- **Azure Cache for Redis**: Fully managed, 99.9% SLA
- **AWS ElastiCache**: Redis or Memcached
- **Google Cloud Memorystore**: Managed Redis
- **Redis Cloud**: Official Redis managed service

**Recommended**: Start with managed service for production, self-hosted for dev/test

### Performance Comparison

| Metric | Redis | SQL Server | Winner |
|--------|-------|------------|--------|
| Job Pickup Latency | <1ms | 10-50ms | ? Redis |
| Job Queue Throughput | 10,000+ jobs/sec | 1,000-5,000 jobs/sec | ? Redis |
| Dashboard Query Speed | Fast (recent), Slow (history) | Fast (all) | Depends |
| Durability | AOF (99.9%) | ACID (100%) | SQL |
| Horizontal Scaling | Excellent (Cluster) | Complex (HA/AG) | ? Redis |
| Memory Footprint | High (all in RAM) | Low (on disk) | SQL |
| Cost | Medium | High | ? Redis |

**Verdict**: Redis is the clear winner for KeryxFlux's use case

### Job History Retention Strategy

**Challenge**: Redis memory is limited; job history grows indefinitely

**Solutions:**

1. **Hangfire Built-in Retention** (Recommended):
```csharp
new RedisStorageOptions
{
    DeletedListSize = 1000,      // Keep only 1000 deleted jobs
    SucceededListSize = 1000     // Keep only 1000 succeeded jobs
}
```

2. **Periodic Cleanup Job**:
```csharp
RecurringJob.AddOrUpdate(
    \"cleanup-old-jobs\",
    () => CleanupOldJobs(),
    Cron.Daily);  // Run daily at midnight

public void CleanupOldJobs()
{
    // Delete job records older than 7 days
    var expireDate = DateTime.UtcNow.AddDays(-7);
    // Hangfire automatically handles cleanup based on DeletedListSize/SucceededListSize
}
```

3. **Export to Long-Term Storage** (Optional for Analytics):
```csharp
// Export Hangfire metrics to SQL/ElasticSearch for long-term reporting
RecurringJob.AddOrUpdate(
    \"export-job-metrics\",
    () => ExportJobMetricsToWarehouse(),
    Cron.Hourly);
```

### Redis Monitoring

**Key Metrics to Track:**

```bash
# Memory usage
redis-cli INFO memory

# Keyspace hits vs misses (cache effectiveness)
redis-cli INFO stats | grep keyspace_hits

# Connected clients
redis-cli INFO clients

# Persistence status
redis-cli INFO persistence
```

**Prometheus Exporter:**
```yaml
# docker-compose.yml
redis-exporter:
  image: oliver006/redis_exporter:latest
  environment:
    - REDIS_ADDR=redis:6379
  ports:
    - \"9121:9121\"  # Prometheus scrape endpoint
```

**Grafana Dashboard**: Import dashboard ID `763` for Redis monitoring

### Backup Strategy

**Redis AOF Files:**
```bash
# Automated backup of AOF file
0 2 * * * docker exec redis redis-cli BGSAVE
0 3 * * * cp /var/lib/redis/appendonly.aof /backups/redis-$(date +\\%Y\\%m\\%d).aof
```

**Alternative**: Use cloud provider's automated backup
- Azure Cache: Automated daily backups with retention
- AWS ElastiCache: Automated snapshots to S3

---

## Scalability & High Availability

## Scalability & High Availability

### Horizontal Scaling Matrix

| Component | Scaling Strategy | Notes |
|-----------|------------------|-------|
| **Main Instance** | Active-Standby (1 active) | Use Kubernetes leader election for HA |
| **Node Instances** | Active-Active (N workers) | Stateless, scale to hundreds of nodes |
| **HTTP Receivers** | Load Balanced | All nodes behind ALB/Ingress |
| **TCP Receivers** | Single Active Node | Leader election required (only 1 can bind port) |
| **RabbitMQ Consumers** | Competing Consumers | All nodes consume, RabbitMQ distributes |
| **Kafka Consumers** | Consumer Group | Kafka partitions distributed across nodes |
| **Hangfire Jobs** | Distributed Processing | All workers compete for jobs from queue |

### Failure Scenarios & Mitigation

| Scenario | Impact | Recovery | Mitigation |
|----------|--------|----------|------------|
| **Main crashes** | No new jobs scheduled | K8s restarts pod | Standby Main via leader election |
| **Node crashes** | In-flight jobs fail | Hangfire retries on another node | Job idempotency required |
| **Redis crashes** | No docket sync | Nodes use cached dockets | Redis persistence + backups |
| **SQL Server crashes** | Hangfire stops | Jobs resume when SQL recovers | SQL HA/Always On |
| **RabbitMQ crashes** | Messages lost | Client reconnect | Persistent queues + mirroring |
| **Kafka crashes** | Consumer lag increases | Consumer rebalance | Multi-broker replication |
| **Plugin crash** | Job fails | Retry with backoff | Plugin isolation (future) |

### Kubernetes Deployment Strategy

**High Availability Setup:**

```yaml
# KeryxFlux-main deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: KeryxFlux-main
  namespace: KeryxFlux
spec:
  replicas: 2  # Active-Standby via leader election
  selector:
    matchLabels:
      app: KeryxFlux
      mode: main
  template:
    metadata:
      labels:
        app: KeryxFlux
        mode: main
    spec:
      containers:
      - name: KeryxFlux
        image: KeryxFlux:1.0.0
        env:
        - name: KeryxFlux_MODE
          value: "main"
        - name: ENABLE_LEADER_ELECTION
          value: "true"
        volumeMounts:
        - name: dockets
          mountPath: /app/dockets
        - name: plugins
          mountPath: /app/plugins
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5

---
# KeryxFlux-node deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: KeryxFlux-node
  namespace: KeryxFlux
spec:
  replicas: 5  # Scale horizontally based on load
  selector:
    matchLabels:
      app: KeryxFlux
      mode: node
  template:
    metadata:
      labels:
        app: KeryxFlux
        mode: node
    spec:
      containers:
      - name: KeryxFlux
        image: KeryxFlux:1.0.0
        env:
        - name: KeryxFlux_MODE
          value: "node"
        volumeMounts:
        - name: dockets
          mountPath: /app/dockets
        - name: plugins
          mountPath: /app/plugins
        resources:
          requests:
            memory: "1Gi"
            cpu: "1000m"
          limits:
            memory: "2Gi"
            cpu: "2000m"

---
# HorizontalPodAutoscaler for nodes
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: KeryxFlux-node-hpa
  namespace: KeryxFlux
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: KeryxFlux-node
  minReplicas: 3
  maxReplicas: 20
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80

---
# Service for HTTP receivers (LoadBalancer)
apiVersion: v1
kind: Service
metadata:
  name: KeryxFlux-http
  namespace: KeryxFlux
spec:
  type: LoadBalancer
  selector:
    app: KeryxFlux
    mode: node  # Only route to nodes
  ports:
  - name: http
    port: 80
    targetPort: 8080
    protocol: TCP

---
# Service for Main instance (for Hangfire dashboard)
apiVersion: v1
kind: Service
metadata:
  name: KeryxFlux-main
  namespace: KeryxFlux
spec:
  type: ClusterIP
  selector:
    app: KeryxFlux
    mode: main
  ports:
  - name: http
    port: 8080
    targetPort: 8080

---
# Ingress for external access
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: KeryxFlux-ingress
  namespace: KeryxFlux
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
spec:
  rules:
  - host: KeryxFlux.example.com
    http:
      paths:
      - path: /receive
        pathType: Prefix
        backend:
          service:
            name: KeryxFlux-http
            port:
              number: 80
      - path: /hangfire
        pathType: Prefix
        backend:
          service:
            name: KeryxFlux-main
            port:
              number: 8080
```

### Performance Targets

| Metric | Target | Measurement |
|--------|--------|-------------|
| **HTTP Receiver Throughput** | >1,000 req/sec per node | Load testing with k6/JMeter |
| **Poller Execution Latency** | <5 sec (p95) for simple transforms | Hangfire metrics |
| **Message Queue Processing** | >500 msg/sec per node | RabbitMQ/Kafka metrics |
| **Horizontal Scaling Efficiency** | Linear up to 10 nodes | Load testing |
| **Plugin Load Time** | <1 sec | Application metrics |
| **Docket Hot-Reload** | <10 sec propagation | Integration testing |

---

## Observability Strategy

### Telemetry Stack

```
???????????????????????
?   KeryxFlux App    ?
???????????????????????
           ?
           ???????????????????????????????????????????
           ?                                         ?
           ?                                         ?
    ???????????????                          ???????????????
    ?   Logs      ?                          ?   Metrics   ?
    ?  (Serilog)  ?                          ?(Prometheus) ?
    ???????????????                          ???????????????
           ?                                         ?
           ?                                         ?
    ???????????????                          ???????????????
    ? Seq / ELK   ?                          ?  Grafana    ?
    ???????????????                          ???????????????
           
           ?
    ???????????????
    ?   Traces    ?
    ? (OpenTel)   ?
    ???????????????
           ?
           ?
    ???????????????
    ?   Jaeger    ?
    ???????????????
```

### Structured Logging (Serilog)

```csharp
// Conceptual logging configuration
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithProperty("Application", "KeryxFlux")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .WriteTo.Seq(serverUrl: "http://seq:5341")
    .WriteTo.File("logs/KeryxFlux-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

// Usage in code
_logger.LogInformation(
    "Processing docket {DocketName} with plugin {PluginName} for endpoint {Endpoint}",
    docketName,
    pluginName,
    endpoint);
```

### Metrics (Prometheus)

**Key Metrics to Track:**

```csharp
// Conceptual metrics (not implementation)

// Docket execution metrics
public static class DocketMetrics
{
    public static readonly Counter ExecutionsTotal = Metrics.CreateCounter(
        "KeryxFlux_docket_executions_total",
        "Total number of docket executions",
        new CounterConfiguration
        {
            LabelNames = new[] { "docket_name", "status" }  // success | failure
        });
    
    public static readonly Histogram ExecutionDuration = Metrics.CreateHistogram(
        "KeryxFlux_docket_duration_seconds",
        "Duration of docket execution",
        new HistogramConfiguration
        {
            LabelNames = new[] { "docket_name" },
            Buckets = Histogram.ExponentialBuckets(0.1, 2, 10)
        });
    
    public static readonly Counter TransformationErrors = Metrics.CreateCounter(
        "KeryxFlux_transformation_errors_total",
        "Total transformation errors",
        new CounterConfiguration
        {
            LabelNames = new[] { "docket_name", "error_type" }
        });
}

// Receiver metrics
public static class ReceiverMetrics
{
    public static readonly Counter RequestsTotal = Metrics.CreateCounter(
        "KeryxFlux_receiver_requests_total",
        "Total HTTP receiver requests",
        new CounterConfiguration
        {
            LabelNames = new[] { "endpoint", "status_code", "method" }
        });
    
    public static readonly Gauge QueueDepth = Metrics.CreateGauge(
        "KeryxFlux_receiver_queue_depth",
        "Current queue depth",
        new GaugeConfiguration
        {
            LabelNames = new[] { "queue_name" }
        });
}

// Sender metrics
public static class SenderMetrics
{
    public static readonly Counter AttemptsTotal = Metrics.CreateCounter(
        "KeryxFlux_sender_attempts_total",
        "Total forwarding attempts",
        new CounterConfiguration
        {
            LabelNames = new[] { "destination", "success" }
        });
    
    public static readonly Histogram Latency = Metrics.CreateHistogram(
        "KeryxFlux_sender_latency_seconds",
        "Forwarding latency",
        new HistogramConfiguration
        {
            LabelNames = new[] { "destination" }
        });
}

// Plugin metrics
public static class PluginMetrics
{
    public static readonly Counter LoadsTotal = Metrics.CreateCounter(
        "KeryxFlux_plugin_loads_total",
        "Total plugin loads",
        new CounterConfiguration
        {
            LabelNames = new[] { "plugin_name", "status" }
        });
    
    public static readonly Gauge LoadedPlugins = Metrics.CreateGauge(
        "KeryxFlux_plugins_loaded",
        "Number of currently loaded plugins");
}
```

**Prometheus Endpoint:**
```csharp
// Expose metrics at /metrics
app.MapMetrics();  // Using prometheus-net library
```

**Grafana Dashboard:**
- Import pre-built dashboard from `docs/grafana-dashboard.json`
- Metrics include: throughput, latency, error rates, queue depths

### Distributed Tracing (OpenTelemetry)

```csharp
// Conceptual OpenTelemetry configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource("KeryxFlux")
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("KeryxFlux")
                .AddTelemetrySdk())
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "jaeger";
                options.AgentPort = 6831;
            });
    });

// Usage in code
using var activity = ActivitySource.StartActivity("ProcessDocket");
activity?.SetTag("docket.name", docketName);
activity?.SetTag("plugin.name", pluginName);

// Trace propagation across HTTP calls automatically handled
```

### Health Check Endpoints

```csharp
// Conceptual health check implementation
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,  // Liveness: just check process is alive
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => true,  // Readiness: check dependencies
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Health checks registered
builder.Services.AddHealthChecks()
    .AddCheck<HangfireHealthCheck>("hangfire")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<DocketManagerHealthCheck>("dockets")
    .AddCheck<PluginManagerHealthCheck>("plugins");
```

**Health Check Response Example:**
```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234",
  "entries": {
    "hangfire": {
      "status": "Healthy",
      "description": "Hangfire connection active"
    },
    "redis": {
      "status": "Healthy",
      "description": "Redis connected"
    },
    "dockets": {
      "status": "Healthy",
      "description": "5 dockets loaded"
    },
    "plugins": {
      "status": "Healthy",
      "description": "3 plugins loaded"
    }
  }
}
```

---

## Security Considerations

### Attack Surface Analysis

| Attack Vector | Risk Level | Mitigation Strategy |
|---------------|------------|---------------------|
| HTTP Receiver DDoS | ?? High | Rate limiting, WAF, CloudFlare |
| HTTP Injection (SQLi, XSS) | ?? Medium | Input validation, sanitization |
| TCP Buffer Overflow | ?? Medium | Message size limits, timeouts |
| Malicious Plugin | ?? High | Plugin sandboxing, code signing |
| YAML Injection | ?? Medium | Schema validation, whitelist |
| Credential Exposure | ?? High | Secrets management, env vars only |
| Unauthorized API Access | ?? Medium | API keys, OAuth2 |
| Man-in-the-Middle | ?? Medium | TLS/HTTPS only, cert validation |

### Secrets Management

**Environment Variable Pattern (Recommended):**

```yaml
# Docket YAML - NEVER store secrets directly
authentication:
  type: oauth2
  client_id_env: EPIC_CLIENT_ID      # Read from environment variable
  client_secret_env: EPIC_SECRET     # NEVER hardcode secrets
```

**Integration Options:**

| Solution | Use Case | Complexity |
|----------|----------|------------|
| **Environment Variables** | Dev/simple deployments | Low |
| **Docker Secrets** | Docker Swarm | Medium |
| **Kubernetes Secrets** | Kubernetes | Medium |
| **Azure Key Vault** | Azure cloud | High |
| **HashiCorp Vault** | On-prem/multi-cloud | High |

**Kubernetes Secrets Example:**

```yaml
# Create secret
apiVersion: v1
kind: Secret
metadata:
  name: KeryxFlux-secrets
  namespace: KeryxFlux
type: Opaque
stringData:
  EPIC_CLIENT_ID: "abc123"
  EPIC_SECRET: "supersecret"
  RABBITMQ_CONNECTION: "amqp://user:pass@rabbitmq:5672"

---
# Reference in deployment
spec:
  containers:
  - name: KeryxFlux
    envFrom:
    - secretRef:
        name: KeryxFlux-secrets
```

### Authentication & Authorization

**HTTP Receiver Authentication:**

```yaml
# API Key
authentication:
  type: api_key
  header: X-API-Key
  secret_env: HL7_API_KEY

# Bearer Token
authentication:
  type: bearer
  secret_env: BEARER_TOKEN

# OAuth2
authentication:
  type: oauth2
  issuer: https://auth.example.com
  audience: KeryxFlux-api
  required_scopes:
    - KeryxFlux:write
```

**Management API Authorization:**

```csharp
// Conceptual authorization
app.MapPost("/api/dockets", [Authorize(Roles = "Admin")] async (Docket docket) =>
{
    // Only admins can create dockets
});

app.MapGet("/api/dockets", [Authorize] async () =>
{
    // Any authenticated user can view dockets
});
```

### Plugin Sandboxing (Future Enhancement)

**Phase 1 (MVP): Basic Isolation**
- Separate `AssemblyLoadContext` per plugin
- Exception handling prevents crash
- Resource limits not enforced

**Phase 2 (Future): Enhanced Isolation**
- Run plugins in separate process
- Use gRPC for communication
- Enforce CPU/memory limits
- Container-per-plugin option

```csharp
// Conceptual future implementation
public class SandboxedPluginExecutor
{
    public async Task<TransformResult> ExecuteAsync(byte[] data, PluginConfig config)
    {
        // Start plugin in isolated process with resource limits
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"KeryxFlux.PluginHost.dll --plugin {config.Path}",
                ResourceLimits = new ResourceLimits
                {
                    MaxMemoryMB = 512,
                    MaxCpuPercent = 50,
                    TimeoutSeconds = 30
                }
            }
        };
        
        // Execute and get result via gRPC
    }
}
```

---

## Development Workflow

### Local Development Setup

```bash
# 1. Clone repository
git clone https://github.com/theelevators/KeryxFlux.git
cd KeryxFlux

# 2. Start dependencies (Redis, SQL, RabbitMQ)
docker-compose -f docker-compose.dev.yml up -d

# 3. Restore packages
dotnet restore

# 4. Run migrations (Hangfire database setup)
dotnet ef database update --project src/KeryxFlux.Infrastructure

# 5. Run Main mode
cd src/KeryxFlux.Host
dotnet run -- --mode main --urls http://localhost:8080

# 6. Run Node mode (separate terminal)
dotnet run -- --mode node --urls http://localhost:8081
```

**docker-compose.dev.yml:**
```yaml
version: '3.8'

services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=Dev!Pass123
    ports:
      - "1433:1433"
      
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
      
  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      - RABBITMQ_DEFAULT_USER=dev
      - RABBITMQ_DEFAULT_PASS=dev
      
  seq:
    image: datalust/seq:latest
    environment:
      - ACCEPT_EULA=Y
    ports:
      - "5341:80"
      
  jaeger:
    image: jaegertracing/all-in-one:latest
    ports:
      - "6831:6831/udp"
      - "16686:16686"
```

### Testing Strategy

**Project Structure:**
```
tests/
??? KeryxFlux.Domain.Tests/           # Unit tests (pure domain logic)
??? KeryxFlux.Application.Tests/      # Unit tests (use cases, orchestration)
??? KeryxFlux.Infrastructure.Tests/   # Integration tests (adapters)
??? KeryxFlux.E2E.Tests/              # End-to-end tests
```

**Unit Tests:**
```csharp
// Example: Domain model validation
[Fact]
public void Docket_Validation_Requires_Name()
{
    var docket = new Docket { Name = "" };
    var result = docket.Validate();
    Assert.True(result.IsFailure);
    Assert.Contains("Name is required", result.Error);
}

// Example: Application service
[Fact]
public async Task DocketManager_LoadDocket_Success()
{
    var manager = new DocketManager(/* mocks */);
    var result = await manager.LoadDocketAsync("/path/to/docket.yaml");
    Assert.True(result.IsSuccess);
    Assert.Equal("test-docket", result.Value.Name);
}
```

**Integration Tests (using Testcontainers):**
```csharp
// Example: RabbitMQ sender integration test
public class RabbitMQSenderTests : IAsyncLifetime
{
    private RabbitMqContainer _rabbitContainer;
    
    public async Task InitializeAsync()
    {
        _rabbitContainer = new RabbitMqBuilder().Build();
        await _rabbitContainer.StartAsync();
    }
    
    [Fact]
    public async Task SendMessage_Success()
    {
        var sender = new RabbitMQSenderService(/* connection */);
        var result = await sender.SendAsync(data, config);
        Assert.True(result.IsSuccess);
    }
    
    public async Task DisposeAsync()
    {
        await _rabbitContainer.DisposeAsync();
    }
}
```

**End-to-End Tests:**
```csharp
// Example: Full workflow test
[Fact]
public async Task EndToEnd_HttpReceiver_Transform_Forward_Success()
{
    // 1. Load docket
    await DocketManager.LoadDocketAsync("test-docket.yaml");
    
    // 2. Send HTTP request to receiver
    var client = new HttpClient();
    var response = await client.PostAsync(
        "http://localhost:8080/receive/test",
        new StringContent("{\"data\":\"test\"}"));
    
    // 3. Assert response
    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    
    // 4. Verify message was forwarded (check destination queue)
    // (using test doubles or actual RabbitMQ)
}
```

### CI/CD Pipeline

**GitHub Actions Workflow:**

```yaml
# .github/workflows/ci.yml
name: CI/CD

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET 10
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '10.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore --configuration Release
    
    - name: Test
      run: dotnet test --no-build --configuration Release --verbosity normal --collect:"XPlat Code Coverage"
    
    - name: Code Coverage Report
      uses: codecov/codecov-action@v3
      with:
        files: ./coverage.cobertura.xml
  
  docker-build:
    needs: build-and-test
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Build Docker Image
      run: docker build -t KeryxFlux:${{ github.sha }} .
    
    - name: Security Scan (Trivy)
      uses: aquasecurity/trivy-action@master
      with:
        image-ref: KeryxFlux:${{ github.sha }}
        format: 'sarif'
        output: 'trivy-results.sarif'
    
    - name: Push to Registry
      if: github.ref == 'refs/heads/main'
      run: |
        echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
        docker tag KeryxFlux:${{ github.sha }} KeryxFlux:latest
        docker push KeryxFlux:latest
  
  deploy-staging:
    needs: docker-build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    
    steps:
    - name: Deploy to Kubernetes Staging
      uses: azure/k8s-deploy@v4
      with:
        manifests: |
          k8s/staging/deployment.yaml
        images: |
          KeryxFlux:${{ github.sha }}
```

---

## Migration Path from ReqStr

### Phase 1: Repository Setup & Restructuring (Week 1-2)

**Tasks:**
- ? Create new GitHub repository: `KeryxFlux`
- ? Setup base solution structure (Domain, Application, Infrastructure, Host)
- ? Migrate useful code from ReqStr:
  - Models ? Domain/Models
  - Abstractions ? Domain/Ports
  - Utils ? Infrastructure/Plugins
- ? Update all namespaces: `ReqStr` ? `KeryxFlux`
- ? Upgrade to .NET 10
- ? Create `MIGRATION.md` documenting changes

**Deliverables:**
- New repository with DDD/Hexagonal structure
- All existing functionality preserved
- Updated namespaces and project references

---

### Phase 2: Core Infrastructure (Week 3-4)

**Tasks:**
- ? Implement `KeryxFlux.Host` with Main/Node mode switching
- ? Integrate Hangfire with SQL Server storage
- ? Implement Redis-based docket synchronization
- ? Build `ReceiverRegistry` and `SenderRegistry`
- ? Create enhanced YAML schema with validation
- ? Implement `DocketFileWatcher` with hot-reload

**Deliverables:**
- Single executable that runs in Main or Node mode
- Docket hot-reload working
- Basic Hangfire scheduling functional

---

### Phase 3: HTTP Receiver & Minimal API (Week 5-6)

**Tasks:**
- ? Implement HTTP catch-all endpoint (`/receive/{**path}`)
- ? Build endpoint-to-docket routing logic
- ? Implement authentication (API Key, Bearer, OAuth2)
- ? Add rate limiting (Redis-based)
- ? Create sample HTTP receiver docket
- ? End-to-end test: HTTP ? Transform ? Forward

**Deliverables:**
- Working HTTP receiver accepting requests
- Authentication and rate limiting functional
- First complete bi-directional flow working

---

### Phase 4: Additional Receivers (Week 7-8)

**Tasks:**
- ? Implement `TcpReceiverService` with MLLP support
- ? Implement `RabbitMQReceiverService` as hosted service
- ? Implement `KafkaReceiverService` as hosted service
- ? Create sample dockets for each receiver type
- ? Integration tests for each receiver

**Deliverables:**
- All 4 receiver types functional (HTTP, TCP, RabbitMQ, Kafka)
- Sample dockets for each type
- Integration test suite passing

---

### Phase 5: Senders & Forwarding (Week 9-10)

**Tasks:**
- ? Implement `HttpSenderService` with retry logic (Polly)
- ? Implement `RabbitMQSenderService` with connection pooling
- ? Implement `KafkaSenderService` with batching
- ? Implement `TcpSenderService`
- ? Build `SenderRegistry` with dynamic routing
- ? Add forwarding destination validation

**Deliverables:**
- All sender services functional
- Retry logic with exponential backoff
- Connection pooling for performance

---

### Phase 6: Observability & Production Readiness (Week 11-12)

**Tasks:**
- ? Integrate Serilog with Seq/Console sinks
- ? Add Prometheus metrics endpoints
- ? Implement OpenTelemetry distributed tracing
- ? Create Grafana dashboard
- ? Add health check endpoints (`/health`, `/ready`)
- ? Create Kubernetes deployment manifests
- ? Load testing with k6 (target: >1000 req/sec)

**Deliverables:**
- Full observability stack operational
- Grafana dashboard for monitoring
- Kubernetes-ready with health checks
- Performance targets met

---

### Phase 7: Documentation & Samples (Week 13-14)

**Tasks:**
- ? Write comprehensive README.md
- ? Create `PLUGIN_DEVELOPMENT.md` guide
- ? Create `DEPLOYMENT.md` with Docker/K8s instructions
- ? Build 3 sample plugins (HL7Parser, FhirTransformer, JsonMapper)
- ? Create example dockets for common scenarios
- ? Record video tutorials (optional)

**Deliverables:**
- Complete documentation
- Sample plugins demonstrating best practices
- Example dockets for common use cases

---

### Phase 8: Security & Hardening (Week 15-16)

**Tasks:**
- ? Implement secrets management (Key Vault/Vault integration)
- ? Add input validation and sanitization
- ? Implement rate limiting per docket
- ? Security audit (OWASP Top 10)
- ? Container security scanning (Trivy)
- ? Penetration testing (optional)

**Deliverables:**
- Production-grade security
- Vulnerability scan reports
- Security documentation

---

**Total Estimated Timeline: 16 weeks (4 months)**

---

## Open Questions & Decisions

### Technical Decisions Required

| Question | Options | Recommendation | Status |
|----------|---------|----------------|--------|
| .NET Version | .NET 8 (LTS) vs .NET 10 (latest) | .NET 10 (future-proof) | ? Pending |
| Database for Hangfire | SQL Server vs PostgreSQL | SQL Server (better support) | ? Pending |
| Message Broker | RabbitMQ only vs Kafka only vs Both | Both (different use cases) | ? Pending |
| Cache | Redis vs In-Memory vs Both | Redis (distributed) | ? Pending |
| Plugin Isolation | AssemblyLoadContext vs Process | AssemblyLoadContext (MVP) | ? Pending |
| Main HA Strategy | Manual failover vs Leader Election | Leader Election (k8s) | ? Pending |
| TCP Receiver Distribution | Single node vs All nodes | Single node (simpler) | ? Pending |
| Monitoring | Prometheus vs App Insights | Prometheus (self-hosted) | ? Pending |

### Product Decisions Required

| Question | Impact | Status |
|----------|--------|--------|
| **Project Name** | Branding, URLs, namespaces | ? Pending (KeryxFlux recommended) |
| Management UI Required? | User experience | ?? Nice-to-have (post-MVP) |
| Plugin Marketplace? | Ecosystem growth | ?? Future consideration |
| SaaS vs Self-Hosted Only? | Business model | ? Pending |
| Commercial vs Open Source? | Licensing, revenue | ? Pending (suggest Apache 2.0 OSS) |
| Support Model | Community vs paid support | ?? Future consideration |

### Operational Decisions Required

| Question | Options | Recommendation | Status |
|----------|---------|----------------|--------|
| Primary deployment target | Docker Compose vs Kubernetes | Both (Compose=dev, K8s=prod) | ? Pending |
| Cloud provider focus | Azure vs AWS vs GCP vs Agnostic | Agnostic with Azure examples | ? Pending |
| Container registry | Docker Hub vs GitHub vs Private | GitHub Container Registry | ? Pending |
| CI/CD Platform | GitHub Actions vs Azure DevOps | GitHub Actions | ? Pending |

---

## Success Criteria

### MVP Definition

A successful MVP must demonstrate:

? **Single Docker Image** - Runs as Main or Node based on env var  
? **HTTP Receiver** - Catch-all endpoint with routing validation  
? **Scheduled Poller** - Executes on cron via Hangfire  
? **Bi-Directional Flow** - Both receive and send data  
? **At least 2 Receivers** - HTTP + RabbitMQ working  
? **At least 2 Senders** - HTTP + RabbitMQ working  
? **Sample Plugin** - Demonstrates transformation  
? **Horizontal Scaling** - 1 Main + 3+ Nodes working together  
? **Docket Hot-Reload** - Changes propagate without restart  
? **Basic Observability** - Logs, metrics, traces functional  
? **Documentation** - Architecture, deployment, plugin dev guides  

### Performance Targets

| Metric | Target | Measurement Method |
|--------|--------|-------------------|
| HTTP Receiver Throughput | >1,000 req/sec per node | k6 load testing |
| Poller Execution Latency | <5 sec (p95) for simple transforms | Hangfire dashboard metrics |
| Message Queue Processing | >500 msg/sec per node | RabbitMQ/Kafka monitoring |
| Horizontal Scaling Efficiency | Linear up to 10 nodes | Load test with increasing nodes |
| Plugin Load Time | <1 sec | Application metrics |
| Docket Hot-Reload Propagation | <10 sec | Integration testing |
| Memory Usage per Node | <2 GB under load | Prometheus metrics |
| CPU Usage per Node | <70% under load | Prometheus metrics |

### Operational Targets

| Metric | Target | Notes |
|--------|--------|-------|
| Deployment Time | <5 minutes (git push to production) | CI/CD pipeline |
| Node Recovery Time | <30 seconds from failure | K8s pod restart |
| Docket Deployment Time | <10 seconds (YAML change to active) | Hot-reload mechanism |
| Zero-Downtime Updates | Yes | Rolling deployment strategy |
| RTO (Recovery Time Objective) | <5 minutes | Disaster recovery |
| RPO (Recovery Point Objective) | <1 minute | Data loss tolerance |

---

## Risk Assessment

### Technical Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Plugin crashes bring down host | ?? Medium | ?? High | AppDomain isolation + circuit breaker pattern |
| Docket sync fails across nodes | ?? Medium | ?? Medium | Fallback to file system, Redis retry logic |
| Hangfire SQL becomes bottleneck | ?? Low | ?? High | Read replicas, proper indexing, connection pooling |
| TCP receiver SPOF (single node) | ?? Medium | ?? Medium | Leader election for automatic failover |
| .NET 10 adoption issues | ?? Low | ?? Low | .NET 8 LTS fallback option |
| RabbitMQ/Kafka message loss | ?? Medium | ?? High | Persistent queues, replication, manual ack |
| Memory leaks from plugin loading | ?? Medium | ?? High | Proper disposal, memory profiling, limits |
| Horizontal scaling not linear | ?? Low | ?? Medium | Load testing, profiling, optimization |

### Operational Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Complex deployment for users | ?? Medium | ?? Medium | Comprehensive docs, docker-compose templates |
| Inadequate monitoring/debugging | ?? Medium | ?? High | Full observability stack, detailed logging |
| Security vulnerabilities | ?? Medium | ?? High | Regular scanning, security audits, updates |
| Poor plugin documentation | ?? Medium | ?? Medium | Example plugins, detailed dev guide |
| Lack of community adoption | ?? Medium | ?? Medium | Open source, marketing, showcase projects |

### Business Risks

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Competing with established products | ?? High | ?? Medium | Focus on DevOps-first, cloud-native differentiators |
| Healthcare compliance (HIPAA, GDPR) | ?? Medium | ?? High | Security-first design, audit logging, encryption |
| Insufficient funding/resources | ?? Medium | ?? High | Open source community contributions, sponsorship |
| Scope creep delaying MVP | ?? High | ?? Medium | Strict MVP definition, phased roadmap |

---

## Appendices

### Appendix A: YAML Schema Reference

See section [Configuration & Docket Management](#configuration--docket-management) for full schema examples.

### Appendix B: Plugin Development Guide

See `docs/PLUGIN_DEVELOPMENT.md` (to be created).

### Appendix C: Deployment Guide

See `docs/DEPLOYMENT.md` (to be created).

### Appendix D: Glossary

| Term | Definition |
|------|------------|
| **Docket** | YAML configuration file defining a workflow (receiver or poller) |
| **Plugin** | .NET assembly implementing `IKeryxFluxPlugin` for data transformation |
| **Main Mode** | KeryxFlux instance that schedules jobs and manages dockets |
| **Node Mode** | KeryxFlux instance that processes jobs and handles requests |
| **Receiver** | Inbound data handler (HTTP, TCP, RabbitMQ, Kafka) |
| **Sender** | Outbound data forwarder (HTTP, RabbitMQ, Kafka, TCP) |
| **Poller** | Scheduled workflow that fetches data on a cron schedule |
| **MLLP** | Minimal Lower Layer Protocol (used for HL7 over TCP) |
| **DDD** | Domain-Driven Design (software design approach) |
| **Hexagonal Architecture** | Ports and Adapters pattern for clean architecture |

### Appendix E: Contributing Guidelines

See `CONTRIBUTING.md` (to be created).

### Appendix F: License

To be decided:
- **Option 1:** MIT License (most permissive, max adoption)
- **Option 2:** Apache 2.0 (includes patent protection)
- **Option 3:** AGPL (forces contributions back, protects from SaaS competitors)

**Recommendation:** Apache 2.0 (balance between open and protective)

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2024 | Team | Initial architecture plan |

---

## Next Steps

### Immediate Actions (Before Coding)

1. ? **Review this plan** - Stakeholder approval
2. ?? **Finalize project name** - Vote on: KeryxFlux vs InterFlow vs alternatives
3. ?? **Decide open questions** - .NET version, databases, message brokers
4. ?? **Validate YAML schema** - Review with potential users
5. ?? **Prioritize features** - Confirm MVP scope
6. ?? **Create new repository** - Initialize with proper structure

### First Sprint (Week 1-2)

1. Create `KeryxFlux` repository on GitHub
2. Setup solution structure (Domain, Application, Infrastructure, Host)
3. Migrate useful code from `ReqStr`
4. Update namespaces to `KeryxFlux`
5. Upgrade to .NET 10
6. Create initial README and MIGRATION guide
7. Setup CI/CD pipeline (GitHub Actions)

---

**Questions? Feedback? Ready to start implementation?**

