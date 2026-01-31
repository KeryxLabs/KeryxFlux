# KeryxFlux

> **Part of the [KeryxHealth](https://github.com/theelevators) ecosystem**

**Cloud-native bi-directional interoperability engine for healthcare and enterprise integration**

---

## What is KeryxFlux?

KeryxFlux is a modern, horizontally scalable interoperability engine that enables seamless data flow between disparate systems. Built with DevOps-first principles, it combines the power of declarative YAML configuration with a robust plugin architecture.

**Etymology**: 
- **Keryx** (Greek: ?????) - Herald, messenger
- **Flux** (Latin) - Flow, continuous change

### Key Features

? **Bi-Directional Communication**
- Receive data via HTTP, TCP (MLLP), RabbitMQ, Kafka
- Send data to HTTP endpoints, message queues, event streams

? **DevOps-First**
- YAML-based declarative configuration ("dockets")
- Hot-reload configuration without restarts
- Version control friendly

? **Cloud-Native**
- Single Docker image, multiple deployment modes (Main/Node)
- Horizontal scaling with Kubernetes
- Redis-based distributed state management

? **Plugin System**
- Hot-loadable transformation plugins (.NET assemblies)
- Integrate with [KeryxPars](https://github.com/theelevators/KeryxPars) for message parsing

? **Dynamic Date Templating**
- Time-based URL generation with lookback windows
- Backfill scenarios with configurable time ranges
- Multi-tenant polling with different time offsets

? **Production-Ready**
- Hangfire-powered job scheduling
- OpenTelemetry observability
- Health checks and metrics

---

## Architecture

KeryxFlux follows Domain-Driven Design (DDD) with Hexagonal Architecture:

```
src/
??? KeryxFlux.Domain          # Pure domain logic, models, ports
??? KeryxFlux.Application     # Use cases, orchestration
??? KeryxFlux.Infrastructure  # Adapters (HTTP, RabbitMQ, Kafka, TCP)
??? KeryxFlux.Contracts       # Plugin developer contracts
??? KeryxFlux.Host            # Unified service host
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
git clone https://github.com/theelevators/KeryxFlux.git
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
keryxflux preview patient-sync.yaml

# Create new docket from template
keryxflux create poller -n "my-poller" -o ./dockets

# Debug configuration
keryxflux debug patient-sync.yaml
```  

### Features

- ? **validate** - Validate docket files for errors
- ? **preview** - Preview resolved URLs and variables
- ? **create** - Generate dockets from templates
- ? **debug** - Deep debugging and troubleshooting

**Full documentation**: [CLI README](./src/KeryxFlux.Cli/README.md)

---

## Configuration

### Docket Example (HTTP Receiver)

```yaml
# dockets/hl7-receiver.yaml
name: hl7-admission-receiver
version: 1.0.0
type: receiver
plugin_location: ./plugins/HL7Parser.dll

receiver:
  type: http
  endpoint: /receive/hl7-admissions
  authentication:
    type: api_key
    header: X-API-Key
    secret_env: HL7_API_KEY

forwarding:
  destinations:
    - name: ehr-system
      type: http
      url: https://ehr.example.com/api/admissions
      method: POST
      retry_policy:
        max_attempts: 3
        backoff: exponential
```

### Docket Example (Scheduled Poller)

```yaml
# dockets/patient-sync.yaml
name: patient-sync-poller
version: 1.0.0
type: poller
plugin_location: ./plugins/FhirParser.dll

scheduler:
  cron_expression: "*/15 * * * *"
  queue: default
  server:
    name: epic-fhir
    address: https://fhir.epic.com/Patient
    authentication:
      type: oauth2
      token_url: https://oauth.epic.com/token
      client_id_env: EPIC_CLIENT_ID
      client_secret_env: EPIC_SECRET

forwarding:
  destinations:
    - type: rabbitmq
      connection_env: RABBITMQ_CONNECTION
      exchange: patient-updates
```

### Docket Example (Date Variables - Lookback Window)

```yaml
# dockets/patient-updates-lookback.yaml
name: patient-updates-lookback
version: 1.0.0
type: poller
plugin_location: ./plugins/FhirParser.dll

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
    name: epic-fhir
    # Dynamic URL with date variable
    address: "https://fhir.epic.com/Patient?_lastUpdated=gt{lookback_date}"
    authentication:
      type: oauth2
      token_url: https://oauth.epic.com/token
      client_id_env: EPIC_CLIENT_ID
      client_secret_env: EPIC_SECRET

forwarding:
  destinations:
    - type: http
      url: https://warehouse.internal.com/api/patients
      method: POST
```

**Resolved URL Example** (at 2025-01-15 15:30 UTC):
```
https://fhir.epic.com/Patient?_lastUpdated=gt2025-01-15T14:30:00Z
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

## Related Projects

- **[KeryxPars](https://github.com/theelevators/KeryxPars)**: Interface message parsing library (HL7, FHIR, X12)
- Use KeryxPars inside KeryxFlux plugins for robust message parsing

---

## Documentation

- [Architecture Guide](docs/ARCHITECTURE.md) - Detailed architecture and design decisions
- [Branding Guidelines](docs/BRANDING.md) - KeryxHealth ecosystem branding
- [Redis Decision](docs/REDIS_DECISION.md) - Why we chose Redis for Hangfire
- [Plugin Development](docs/PLUGIN_DEVELOPMENT.md) - Create custom transformations _(coming soon)_
- [Deployment Guide](docs/DEPLOYMENT.md) - Kubernetes, Docker Swarm, cloud deployments _(coming soon)_

---

## Comparison to Alternatives

| Feature | KeryxFlux | Mirth Connect | Rhapsody | Azure Logic Apps |
|---------|-----------|---------------|----------|------------------|
| Cloud-Native | ? | ? | ? | ? |
| DevOps-First (YAML) | ? | ? (GUI) | ? (GUI) | ?? (Portal) |
| Horizontal Scaling | ? | ?? | ?? | ? |
| Open Source | ? | ? | ? | ? |
| Self-Hosted | ? | ? | ? | ? |
| Modern Stack (.NET 8+) | ? | ? (Java) | ? | ? |

---

## Roadmap

### Phase 1 (Current - MVP)
- [x] Core architecture (DDD/Hexagonal)
- [x] Docket YAML configuration
- [x] Plugin system
- [ ] HTTP receiver
- [ ] RabbitMQ sender/receiver
- [ ] Hangfire scheduling
- [ ] Redis integration

### Phase 2 (Q2 2026)
- [ ] TCP/MLLP receiver
- [ ] Kafka sender/receiver
- [ ] Full observability (OpenTelemetry)
- [ ] Kubernetes Helm charts
- [ ] Sample plugins (HL7, FHIR, JSON)

### Phase 3 (Q3 2026)
- [ ] Management UI
- [ ] Plugin marketplace
- [ ] Advanced routing rules
- [ ] Security enhancements

---

## Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup

```bash
# Install .NET 8 SDK
# Clone repo
git clone https://github.com/theelevators/KeryxFlux.git

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

- **Issues**: [GitHub Issues](https://github.com/theelevators/KeryxFlux/issues)
- **Discussions**: [GitHub Discussions](https://github.com/theelevators/KeryxFlux/discussions)
- **Discord**: _Coming soon_

---

## Acknowledgments

Part of the **KeryxHealth** ecosystem, building modern tools for healthcare interoperability.

---

**Built with ?? for the healthcare integration community**

