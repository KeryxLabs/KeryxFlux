# KeryxFlux YAML Configuration Guide

## Naming Convention

**? Use `snake_case` for all YAML properties** (industry standard)

### Why snake_case?

This follows industry standards across major platforms:
- **Kubernetes:** `replica_count`, `image_pull_policy`
- **Docker Compose:** `container_name`, `working_dir`
- **Ansible:** `remote_user`, `become_user`
- **GitHub Actions:** `runs_on`, `working_directory`

---

## Property Mapping

YamlDotNet's `UnderscoredNamingConvention` maps:

| C# Property (PascalCase) | YAML Field (snake_case) |
|--------------------------|-------------------------|
| `Name` | `name` |
| `Version` | `version` |
| `Type` | `type` |
| `PluginLocation` | `plugin_location` |
| `TimeoutSeconds` | `timeout_seconds` |
| `TraceRequests` | `trace_requests` |

---

## Docket Structure

### Top-Level Properties (Flat Structure)

```yaml
# Required fields
name: my-docket
version: 1.0.0
type: Receiver  # or Poller

# Plugin configuration
plugin_location: plugins/MyPlugin.dll

# Receiver configuration (for Receiver type)
receiver:
  type: http
  endpoint: /receive/my-endpoint

# Forwarding configuration
forwarding:
  destinations:
    - name: destination-1
      type: http
      url: https://example.com/webhook
      timeout_seconds: 30

# Optional telemetry
telemetry:
  enabled: true
  trace_requests: true
```

### Nested Objects

**Receiver Configuration:**
```yaml
receiver:
  type: http              # http, rabbitmq, kafka, tcp
  endpoint: /receive/hl7  # HTTP path
```

**Poller/Scheduler Configuration:**
```yaml
scheduler:
  cron_expression: "0 */5 * * * *"  # Every 5 minutes
  max_concurrent_items: 16
  server:
    url: https://api.example.com/patients
    auth_type: oauth2
```

**Forwarding Configuration:**
```yaml
forwarding:
  destinations:
    - name: fhir-server
      type: http
      url: https://fhir.example.com/r4/Bundle
      timeout_seconds: 30
      retry_policy:
        max_attempts: 3
        backoff_strategy: exponential
        initial_delay_seconds: 1
```

---

## Complete Examples

### HTTP Receiver Example

```yaml
name: hl7-to-fhir-receiver
version: 2.1.0
type: Receiver

plugin_location: plugins/KeryxFlux.Plugins.HL7ToFHIR.dll

receiver:
  type: http
  endpoint: /receive/hl7

forwarding:
  destinations:
    - name: fhir-server-prod
      type: http
      url: https://fhir.hospital.com/r4/Bundle
      method: POST
      timeout_seconds: 45
      headers:
        Authorization: Bearer ${FHIR_API_KEY}
        Content-Type: application/fhir+json
      retry_policy:
        max_attempts: 3
        backoff_strategy: exponential
        initial_delay_seconds: 2
        max_delay_seconds: 30

telemetry:
  enabled: true
  trace_requests: true
```

### Poller Example

```yaml
name: epic-patient-sync
version: 1.0.0
type: Poller

plugin_location: plugins/KeryxFlux.Plugins.EpicFHIR.dll

scheduler:
  cron_expression: "0 */15 * * * *"  # Every 15 minutes
  max_concurrent_items: 32
  server:
    url: https://fhir.epic.com/Patient
    auth_type: oauth2
    oauth:
      token_url: https://auth.epic.com/token
      client_id: ${EPIC_CLIENT_ID}
      client_secret: ${EPIC_CLIENT_SECRET}
      scope: patient/*.read

forwarding:
  destinations:
    - name: internal-database
      type: http
      url: https://internal.hospital.com/api/patients
      timeout_seconds: 60

telemetry:
  enabled: true
  trace_requests: true
```

---

## Field Reference

### Required Fields

```yaml
name: string           # Unique docket identifier
version: string        # Semantic version (1.0.0)
type: enum            # Receiver | Poller
plugin_location: string  # Path to plugin DLL
forwarding: object    # At least one destination required
```

### Optional Fields

```yaml
receiver: object          # Required if type: Receiver
scheduler: object         # Required if type: Poller
server_information: object  # Legacy, prefer scheduler.server
telemetry: object         # Monitoring configuration
```

---

## Receiver Types

### HTTP Receiver
```yaml
receiver:
  type: http
  endpoint: /receive/my-endpoint  # URL path
```

### RabbitMQ Receiver
```yaml
receiver:
  type: rabbitmq
  connection_env: RABBITMQ_CONNECTION  # Environment variable
  exchange: hl7-exchange
  routing_key: hl7.messages
  queue: hl7-queue
```

### Kafka Receiver
```yaml
receiver:
  type: kafka
  bootstrap_servers: ${KAFKA_BROKERS}
  topic: patient-events
  consumer_group: keryx-consumers
  partition_key: patient_id
```

### TCP Receiver
```yaml
receiver:
  type: tcp
  host: 0.0.0.0
  port: 5000
  protocol: mllp  # Minimum Lower Layer Protocol for HL7
```

---

## Destination Types

### HTTP Destination
```yaml
- name: webhook-destination
  type: http
  url: https://example.com/webhook
  method: POST  # Optional, defaults to POST
  timeout_seconds: 30
  headers:  # Optional
    Authorization: Bearer token
    X-Custom-Header: value
```

### RabbitMQ Destination
```yaml
- name: rabbitmq-destination
  type: rabbitmq
  connection_env: RABBITMQ_CONNECTION
  exchange: output-exchange
  routing_key: processed.messages
```

### Kafka Destination
```yaml
- name: kafka-destination
  type: kafka
  bootstrap_servers: ${KAFKA_BROKERS}
  topic: processed-events
  partition_key: correlation_id
```

### SFTP Destination
```yaml
- name: sftp-destination
  type: sftp
  host: sftp.example.com
  port: 22
  username: ${SFTP_USER}
  password: ${SFTP_PASSWORD}
  remote_path: /data/processed/
```

---

## Environment Variables

Use `${VARIABLE_NAME}` syntax for sensitive data:

```yaml
forwarding:
  destinations:
    - name: secure-api
      type: http
      url: ${API_URL}  # From environment
      headers:
        Authorization: Bearer ${API_TOKEN}
```

---

## Retry Policy Configuration

```yaml
retry_policy:
  max_attempts: 3                  # Total attempts (including first)
  backoff_strategy: exponential    # exponential | linear | constant
  initial_delay_seconds: 1         # First retry delay
  max_delay_seconds: 60           # Cap for exponential backoff
```

**Backoff Strategies:**
- `exponential`: 1s, 2s, 4s, 8s, 16s, ...
- `linear`: 1s, 2s, 3s, 4s, 5s, ...
- `constant`: 1s, 1s, 1s, 1s, 1s, ...

---

## Validation Rules

### Name
- ? Alphanumeric with hyphens/underscores
- ? Must be unique across all dockets
- ? No spaces or special characters

### Version
- ? Semantic versioning: `MAJOR.MINOR.PATCH`
- ? Examples: `1.0.0`, `2.3.1-beta`, `0.1.0-alpha`

### Type
- ? `Receiver` or `Poller` (case-sensitive)
- ? No other values accepted

### Plugin Location
- ? Must be valid file path
- ? DLL must exist at specified location
- ? Must implement `IKeryxFluxPlugin`

### Endpoint (HTTP Receiver)
- ? Must start with `/`
- ? URL-safe characters only
- ? Example: `/receive/hl7`, `/api/v1/webhook`

### Cron Expression (Poller)
- ? Standard cron format with seconds
- ? Example: `0 */5 * * * *` (every 5 minutes)
- ? Use https://crontab.guru for validation

---

## Best Practices

### 1. Use Descriptive Names
```yaml
# ? Good
name: epic-patient-demographics-sync

# ? Bad
name: docket1
```

### 2. Version Everything
```yaml
# ? Good - track changes
version: 2.3.1

# ? Bad - hard to track
version: 1.0.0  # Never changed
```

### 3. Document with Comments
```yaml
# Patient demographics synchronization from Epic
# Runs every 15 minutes during business hours
name: epic-patient-sync
```

### 4. Use Environment Variables for Secrets
```yaml
# ? Good
url: ${FHIR_SERVER_URL}

# ? Bad - hardcoded secrets
url: https://user:password@example.com
```

### 5. Set Reasonable Timeouts
```yaml
# ? Good - based on expected response time
timeout_seconds: 30

# ? Bad - too short or too long
timeout_seconds: 5    # Too short, may timeout prematurely
timeout_seconds: 300  # Too long, blocks resources
```

---

## Common Mistakes

### ? Nested Plugin Structure
```yaml
# Wrong!
plugin:
  location: plugins/MyPlugin.dll
```

### ? Flat Plugin Property
```yaml
# Correct!
plugin_location: plugins/MyPlugin.dll
```

### ? camelCase or PascalCase
```yaml
# Wrong!
pluginLocation: ...
PluginLocation: ...
```

### ? snake_case
```yaml
# Correct!
plugin_location: ...
```

### ? Missing Required Fields
```yaml
# Wrong - no forwarding!
name: my-docket
type: Receiver
```

### ? All Required Fields
```yaml
# Correct!
name: my-docket
type: Receiver
plugin_location: plugins/...
forwarding:
  destinations: [...]
```

---

## YAML Style Guide

### Indentation
- **2 spaces** (not tabs)
- Consistent throughout file

### Comments
- Use `#` for comments
- Comment complex configurations
- Document cron expressions

### Quotes
- **Optional** for simple strings
- **Required** for:
  - URLs with special characters
  - Environment variables: `"${VAR}"`
  - Cron expressions with spaces

### Arrays
```yaml
# Multiline (preferred for readability)
destinations:
  - name: dest1
    type: http
    url: https://example1.com
  - name: dest2
    type: http
    url: https://example2.com

# Inline (only for simple values)
tags: [patient, demographics, epic]
```

---

**Status:** ? **YAML CONVENTION: snake_case (Industry Standard)**

All docket YAML files should follow this guide for consistency and compatibility.
