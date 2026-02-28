# KeryxFlux

**Cloud-native declarative data orchestration framework for .NET**

---

## What is KeryxFlux?

KeryxFlux is a modern, horizontally scalable data orchestration framework that enables seamless data flow between disparate systems. Built with DevOps-first principles, it combines the power of declarative YAML configuration with a robust plugin architecture.

**Think of it as:**
- Zapier/Make - but self-hosted and code-extensible
- Apache Camel - but cloud-native and YAML-configured  
- AWS EventBridge - but open source and multi-protocol

**Etymology**: 
- **Keryx** (Greek) - Herald, messenger
- **Flux** (Latin) - Flow, continuous change

### Key Features

✨ **Bi-Directional Communication**
- Receive data via HTTP, TCP (MLLP), RabbitMQ, Kafka
- Send data to HTTP endpoints, message queues, event streams

✨ **DevOps-First**
- YAML-based declarative configuration ("dockets")
- Hot-reload configuration without restarts
- Version control friendly

✨ **Cloud-Native**
- Single Docker image, multiple deployment modes (Main/Node)
- Horizontal scaling with Kubernetes
- Redis-based distributed state management

✨ **Plugin System**
- Hot-loadable transformation plugins (.NET assemblies)

✨ **Dynamic Date Templating**
- Time-based URL generation with lookback windows
- Backfill scenarios with configurable time ranges
- Multi-tenant polling with different time offsets

✨ **Production-Ready**
- Hangfire-powered job scheduling
- OpenTelemetry observability
- Health checks and metrics

---


## What KeryxFlux Is (And Isn't)

### ✅ KeryxFlux IS:
- **Generic infrastructure framework** for data orchestration
- **YAML-based configuration** system for integration workflows
- **Multi-protocol adapter** (HTTP, TCP, RabbitMQ, Kafka, etc.)
- **Plugin architecture** for custom transformations
- **Comparable to:** Apache Camel, Zapier, AWS EventBridge, Airflow

### ❌ KeryxFlux IS NOT:
- NOT an industry-specific solution
- NOT a pre-built integration platform
- NOT a turnkey application
- NOT opinionated about your domain

**Think of it like:**
- Express.js → web framework (not a website)
- Entity Framework → ORM library (not a database)
- **KeryxFlux → orchestration framework** (not the orchestrations)

**You build applications ON TOP of KeryxFlux.**

## Architecture

KeryxFlux follows Domain-Driven Design (DDD) with Hexagonal Architecture:

```
src/
 KeryxFlux.Domain          # Pure domain logic, models, ports
 KeryxFlux.Application     # Use cases, orchestration
 KeryxFlux.Infrastructure  # Adapters (HTTP, RabbitMQ, Kafka, TCP)
 KeryxFlux.Contracts       # Plugin developer contracts
 KeryxFlux.Host            # Unified service host
 KeryxFlux.CLI             # Validate, Create, Debug YAML files (Dockets)
```

### Deployment Modes

**Main Mode**: Schedules jobs, manages configuration, hosts Hangfire dashboard  
**Node Mode**: Processes jobs, handles requests, executes transformations

Scale horizontally by adding more nodes while keeping a single Main instance.

---

## Quick Start

### Prerequisites
- .NET 8.0 SDK
- Docker & Docker Compose
- Redis (for distributed state)
- RabbitMQ or Kafka (optional, for messaging)

### Run Locally

```bash
# Clone repository
git clone https://github.com/keryxlabs/KeryxFlux.git
cd KeryxFlux

# Start dependencies
docker-compose up -d

# Run in Main mode
cd src/KeryxFlux.Host
dotnet run -- --mode main

# In another terminal, run a Node
dotnet run -- --mode node --port 8081
```

### Docker Deployment

```bash
# Build image
docker build -t keryxflux:latest .

# Run with Docker Compose
docker-compose up
```

The Main instance runs at `http://localhost:8080` with Hangfire dashboard at `/hangfire`.

---

## KeryxFlux CLI

Manage and validate your docket files with the KeryxFlux command-line tool.

### Installation

```bash
# Install globally
dotnet tool install -g KeryxFlux.Cli

# Verify installation
keryxflux --version
```  

### Quick Start

```bash
# Validate docket files
keryxflux validate ./dockets

# Preview variable resolution
keryxflux preview run-sync.yaml

# Create new docket from template
keryxflux create poller -n "my-poller" -o ./dockets

# Debug configuration
keryxflux debug run-sync.yaml
```  

### Features

- ✔ **validate** - Validate docket files for errors
- ✔ **preview** - Preview resolved URLs and variables
- ✔ **create** - Generate dockets from templates
- ✔ **debug** - Deep debugging and troubleshooting

**Full documentation**: [CLI README](./src/KeryxFlux.Cli/README.md)

---

## Configuration

### Docket Example (HTTP Receiver)

```yaml
# dockets/webhook-receiver.yaml
name: webhook-event-receiver
version: 1.0.0
type: receiver
plugin_location: ./plugins/DataParser.dll

receiver:
  type: http
  endpoint: /receive/webhooks
  authentication:
    type: api_key
    header: X-API-Key
    secret_env: ORG_API_KEY

forwarding:
  destinations:
    - name: processor-system
      type: http
      url: https://processor.example.com/api/events
      method: POST
      retry_policy:
        max_attempts: 3
        backoff: exponential
```

### Docket Example (Scheduled Poller)

```yaml
# dockets/weather-sync.yaml
name: weather-sync-poller
version: 1.0.0
type: poller
plugin_location: ./plugins/WeatherParser.dll

scheduler:
  cron_expression: "*/15 * * * *"
  queue: default
    server:
      name: weather-api
      address: https://api.weather.com/current
      authentication:
        type: api_key
        header: X-API-Key
        secret_env: WEATHER_API_KEY
      client_id_env: EXTERNAL_CLIENT_ID
      client_secret_env: EXTERNAL_SECRET

forwarding:
  destinations:
      - type: rabbitmq
        connection_env: RABBITMQ_CONNECTION
        exchange: weather-updates
```

### Docket Example (Date Variables - Lookback Window)

```yaml
# dockets/weather-updates-lookback.yaml
name: weather-updates-lookback
version: 1.0.0
type: poller
plugin_location: ./plugins/DataParser.dll

# Date variable for 1-hour lookback window
date_variables:
  - name: lookback_date
    offset_expression: "-1h"
    format: "yyyy-MM-dd'T'HH:mm:ss'Z'"
    timezone: "UTC"

scheduler:
  cron_expression: "*/30 * * * *"  # Poll every 30 minutes
  queue: default
  server:
    name: ci-api
      # Dynamic URL with date variable
      address: "https://api.weather.com/history?since={lookback_date}"
    authentication:
      type: oauth2
      token_url: https://api.ci.example/oauth/token
      client_id_env: EXTERNAL_CLIENT_ID
      client_secret_env: EXTERNAL_SECRET

forwarding:
  destinations:
      - type: http
        url: https://datawarehouse.com/api/weather
        method: POST
```

**Resolved URL Example** (at 2025-01-15 15:30 UTC):
```
https://api.weather.com/history?since=2025-01-15T14:30:00Z
```

---

## Plugin Development

Create custom transformations by implementing `IKeryxFluxPlugin`:

```csharp
using KeryxFlux.Contracts;

public class MyCustomPlugin : IKeryxFluxPlugin
{
    public string Name => "MyCustomTransformer";

    public Result<TransformedData> Transform(
        byte[] source, 
        TransformationContext context)
    {
        // Parse incoming data
        var data = Encoding.UTF8.GetString(source);
        
        // Transform logic here
        var transformed = DoSomeTransformation(data);
        
        return new TransformedData(transformed);
    }
}
```

Compile to `.dll` and place in the `plugins/` directory.

---

## Documentation

- [Plugin Development](docs/PLUGIN_DEVELOPMENT.md) - Create custom transformations _(coming soon)_
- [Deployment Guide](docs/DEPLOYMENT.md) - Kubernetes, Docker Swarm, cloud deployments _(coming soon)_

---

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup

```bash
# Install .NET 8 SDK
# Clone repo
git clone https://github.com/keryxlabs/KeryxFlux.git

# Restore packages
dotnet restore

# Build solution
dotnet build

# Run tests
dotnet test
```

---

## License

Apache 2.0 - see [LICENSE](LICENSE) for details.

---

## Support

- **Issues**: [GitHub Issues](https://github.com/keryxlabs/KeryxFlux/issues)
- **Discussions**: [GitHub Discussions](https://github.com/keryxlabs/KeryxFlux/discussions)
- **Discord**: _Coming soon_

---

## Acknowledgments

Part of the **KeryxLabs** ecosystem, building modern tools for data flows.

---

