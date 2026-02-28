# Example Dockets

This directory contains example docket configurations demonstrating KeryxFlux capabilities.

## Available Examples

### Receivers

**HTTP Receiver** (`http-receiver.yaml`)
- Receives webhook events via HTTP endpoint
- Demonstrates: API key authentication, rate limiting
- Forwards to: HTTP, RabbitMQ, Kafka

**RabbitMQ Consumer** (`rabbitmq-consumer.yaml`)
- Consumes messages from RabbitMQ queue
- Demonstrates: Exchange binding, routing keys, durable queues
- Forwards to: HTTP webhook, Kafka topic

**Kafka Consumer** (`kafka-consumer.yaml`)
- Consumes messages from Kafka topic
- Demonstrates: Consumer groups, auto offset reset
- Forwards to: Kafka, RabbitMQ, HTTP

**gRPC Receiver** (`grpc-receiver.yaml`)
- Receives data via gRPC service endpoint
- Demonstrates: gRPC integration, multi-protocol forwarding
- Forwards to: Kafka, RabbitMQ, gRPC

### Pollers

**HTTP Poller** (`poller.yaml`)
- Polls external REST API on schedule
- Demonstrates: Date variables, pagination, multi-step workflows
- Forwards to: HTTP endpoint

### Model-Enhanced

**AI Enricher** (`ai-enricher.yaml`)
- Enriches incoming data using AI model invocations
- Demonstrates: IModelPlugin interface, model endpoint calls
- Requires: Local Ollama or compatible model endpoint
- Forwards to: HTTP endpoint

## Quick Start

1. Copy an example to your `dockets/` directory:
```bash
cp dockets/examples/http-receiver.yaml dockets/my-receiver.yaml
```

2. Edit configuration values:
```yaml
name: my-receiver           # Change this
receiver:
  endpoint: /my-endpoint    # Change this
forwarding:
  destinations:
    - url: http://your-endpoint  # Change this
```

3. Validate the docket:
```bash
keryxflux validate dockets/my-receiver.yaml
```

4. KeryxFlux will auto-load from `dockets/` directory on startup.

## Plugin Requirements

Each docket requires a compiled plugin DLL:

- `http-receiver.yaml` ? Requires plugin implementing `IReceiverPlugin`
- `poller.yaml` ? Requires plugin implementing `IPollerPlugin`
- `ai-enricher.yaml` ? Requires plugin implementing `IModelPlugin`

See `plugins/` directory for example plugin implementations.

## Documentation

- [Architecture Overview](../../README.md#architecture)
- [Plugin Development Guide](../../README.md#plugins)
- [Quick Start Guide](../../README.md#quick-start)
