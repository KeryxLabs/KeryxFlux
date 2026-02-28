# KeryxFlux Examples

This directory contains example configurations and usage patterns for KeryxFlux.

## Structure

```
examples/
??? dockets/              # Example docket YAML configurations
?   ??? sample-receiver.yaml
?   ??? webhook-transformer.yaml
?   ??? ci-job-poller.yaml
??? plugins/              # Example plugin implementations
??? scripts/              # Helper scripts for testing
```

## Quick Start

### 1. Configure a Docket

Copy an example docket from `examples/dockets/` to your dockets directory:

```bash
cp examples/dockets/sample-receiver.yaml dockets/
```

### 2. Update the Configuration

Edit the copied docket file and update:
- Plugin location
- Forwarding destinations (URLs, connection strings)
- Any environment-specific settings

### 3. Load the Docket

The docket will be automatically loaded when KeryxFlux starts if placed in the `dockets/` directory.

## Example Dockets

### HTTP Receiver (`sample-receiver.yaml`)

Simple HTTP receiver that transforms incoming JSON with a metadata envelope.

**Use case:** Accept JSON payloads and forward to webhook.site for testing

**Test:**
```bash
curl -X POST http://localhost:5000/receive/sample \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}'
```

### Webhook Event Transformer

Receives webhook events and transforms them for processing.

**Use case:** Integration with webhook-based systems

### CI Job Poller

Polls CI API for job updates and forwards to downstream systems.

**Use case:** Scheduled job data synchronization

## Creating Your Own Docket

1. Start with an example docket as a template
2. Create your plugin (see `plugins/` directory)
3. Configure the docket YAML:
   - Set the plugin location
   - Define receiver or poller configuration
   - Configure forwarding destinations
4. Test with the provided scripts

## Testing

Use the provided test scripts:

```bash
# PowerShell
.\examples\scripts\test-receiver.ps1

# Bash
./examples/scripts/test-receiver.sh
```

## Best Practices

- ? Use environment variables for sensitive data (API keys, connection strings)
- ? Version your dockets alongside your plugins
- ? Test dockets in isolation before deploying
- ? Use descriptive names for dockets and destinations
- ? Document any custom metadata fields your plugins use

## Need Help?

See the main documentation in `/docs` for detailed guides on:
- Creating plugins
- Docket configuration reference
- Deployment strategies
- Monitoring and troubleshooting
