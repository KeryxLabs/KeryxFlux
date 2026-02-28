# Example Docket Files

This directory contains example docket configurations for KeryxFlux.

## Available Examples

### Receivers (Inbound)
- **`http-receiver.yaml`** - Receives Event messages via HTTP POST
- **`rabbitmq-consumer.yaml`** - Consumes orders from RabbitMQ queue
- **`kafka-consumer.yaml`** - Consumes events from Kafka topic _(coming soon)_
- **`tcp-receiver.yaml`** - Receives Event messages via TCP/MLLP _(coming soon)_

### Pollers (Scheduled Outbound)
- **`poller.yaml`** - Scheduled API job sync every 15 minutes

## Using These Examples

1. Copy an example file to the `/dockets` directory:
   ```bash
   cp dockets/examples/http-receiver.yaml dockets/my-receiver.yaml
   ```

2. Edit the configuration:
   - Update `name` to be unique
   - Set your `plugin_location`
   - Configure authentication with environment variables
   - Update destination URLs

3. Set required environment variables:
   ```bash
   export HL7_API_KEY="your-api-key"
   export EHR_TOKEN="your-bearer-token"
   export RABBITMQ_CONNECTION="amqp://user:pass@localhost:5672"
   ```

4. Place your plugin DLL in `/plugins` directory

5. Restart KeryxFlux or wait for hot-reload (Main mode)

## Configuration Reference

See [ARCHITECTURE.md](../../docs/ARCHITECTURE.md) for detailed YAML schema documentation.

## Creating Custom Dockets

Docket files must end with `-docket.yaml` or `-docket.yml` to be automatically discovered.

**Naming convention:**
```
dockets/
??? Event-receiver-docket.yaml       ? Discovered
??? job-sync-docket.yml        ? Discovered
??? my-config.yaml                 ? Not discovered (missing suffix)
??? test-docket.yaml               ? Discovered
```
