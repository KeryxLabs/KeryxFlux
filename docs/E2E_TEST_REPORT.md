# ?? End-to-End Test Report

## Test Execution Summary

**Date:** 2026-01-29  
**Status:** ? **READY TO RUN**  
**Build:** ? **SUCCESS**  

---

## Pre-Test Setup

### ? Step 1: Dockets Directory Created
```
Directory: D:\Health\KeryxFlux\dockets
```

### ? Step 2: Sample Docket Copied
```
Source: examples\dockets\sample-receiver.yaml
Target: dockets\sample-receiver.yaml
```

### ? Step 3: Build Successful
All projects compiled without errors.

---

## Manual Test Steps

### 1. **Update Webhook URL** (Required)

Get a webhook URL from https://webhook.site and update:

```yaml
# dockets/sample-receiver.yaml
forwarding:
  destinations:
    - name: webhook-site
      type: http
      url: https://webhook.site/YOUR-UNIQUE-ID  # ? Update this!
```

### 2. **Run the Host**

```bash
dotnet run --project src/KeryxFlux.Host
```

**Expected Output:**
```
info: KeryxFlux.Application.Services.DocketOrchestrationService[0]
      Starting DocketOrchestrationService
info: KeryxFlux.Application.FileSystem.DocketMonitor[0]
      Starting DocketMonitor for directory: D:\Health\KeryxFlux\dockets
info: KeryxFlux.Application.FileSystem.DocketMonitor[0]
      Scanning for existing docket files...
info: KeryxFlux.Application.FileSystem.DocketMonitor[0]
      Found 1 docket files to load
info: KeryxFlux.Application.FileSystem.DocketMonitor[0]
      Successfully loaded docket 'sample-receiver' from ...
info: KeryxFlux.Application.Services.DocketOrchestrationService[0]
      Docket loaded: sample-receiver (Type: Receiver)
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 3. **Test Health Endpoint**

In a **new terminal**:

```bash
curl http://localhost:5000/health
```

**Expected:**
```json
{
  "status": "healthy",
  "timestamp": "2026-01-29T..."
}
```

### 4. **List Loaded Dockets**

```bash
curl http://localhost:5000/dockets
```

**Expected:**
```json
{
  "receivers": [
    {
      "name": "sample-receiver",
      "version": "1.0.0",
      "type": "Receiver",
      "endpoint": "/receive/sample",
      "plugin": "KeryxFlux.Plugins.SampleReceiver.dll"
    }
  ],
  "pollers": []
}
```

### 5. **Send Test Message**

```bash
curl -X POST http://localhost:5000/receive/sample \
  -H "Content-Type: application/json" \
  -H "X-Correlation-Id: test-e2e-001" \
  -d '{
    "patientId": "PAT-12345",
    "firstName": "John",
    "lastName": "Doe",
    "dateOfBirth": "1980-05-15",
    "diagnosis": "Type 2 Diabetes"
  }'
```

**Expected Response:**
```json
{
  "status": "accepted",
  "docket": "sample-receiver",
  "correlationId": "test-e2e-001",
  "destinationsForwarded": 1
}
```

### 6. **Verify on Webhook.site**

Check your webhook.site URL - you should see:

```json
{
  "metadata": {
    "receivedAt": "2026-01-29T...",
    "docket": "sample-receiver",
    "correlationId": "test-e2e-001",
    "source": "http",
    "pluginName": "SampleReceiverPlugin",
    "pluginVersion": "1.0.0"
  },
  "originalPayload": {
    "patientId": "PAT-12345",
    "firstName": "John",
    "lastName": "Doe",
    "dateOfBirth": "1980-05-15",
    "diagnosis": "Type 2 Diabetes"
  }
}
```

---

## Test Scenarios

### Scenario 1: Basic Flow
- ? Docket loads on startup
- ? Endpoint registered dynamically (catch-all)
- ? Message received
- ? Plugin transforms (adds metadata)
- ? Message forwarded to webhook
- ? Response returned

### Scenario 2: Invalid Path
```bash
curl -X POST http://localhost:5000/invalid/path \
  -H "Content-Type: application/json" \
  -d '{"test": "data"}'
```

**Expected:**
```json
{
  "error": "No receiver configured for this path"
}
```
**HTTP Status:** 404

### Scenario 3: Hot Reload

1. **Edit** `dockets/sample-receiver.yaml` (change version to 1.0.1)
2. **Save** the file
3. **Check logs** - should see:
   ```
   info: DocketOrchestrationService[0]
         Docket reloaded: sample-receiver
   ```
4. **Test again** - should work with new config

### Scenario 4: Missing Plugin

1. Rename plugin DLL temporarily
2. Create new docket that references missing plugin
3. Should see error in logs but service continues

### Scenario 5: Multiple Dockets

Create `dockets/second-receiver.yaml`:
```yaml
name: second-receiver
version: 1.0.0
type: Receiver
plugin:
  location: plugins/KeryxFlux.Plugins.SampleReceiver.dll
receiver:
  type: http
  endpoint: /receive/second
forwarding:
  destinations:
    - name: webhook-site
      type: http
      url: https://webhook.site/YOUR-ID
```

Both endpoints should work:
- POST /receive/sample ? sample-receiver docket
- POST /receive/second ? second-receiver docket

---

## Expected Log Flow

### On Startup
```
1. DocketOrchestrationService starts
2. DocketMonitor starts
3. Scans dockets/ directory
4. Finds sample-receiver.yaml
5. Loads docket
6. DocketManager stores docket
7. OrchestrationService logs "Docket loaded"
8. Web host starts listening
```

### On Message Received
```
1. POST /receive/sample hits catch-all route
2. Query DocketManager for matching docket
3. Create ReceivedMessage
4. Send ProcessMessageCommand via MediatR
5. ProcessMessageCommandHandler:
   a. Get docket from DocketManager
   b. Load plugin via PluginManager
   c. Transform message via plugin
   d. Get destinations from docket
   e. Send to each destination via HttpSender
6. Return response
```

### On Hot Reload
```
1. FileSystemWatcher detects file change
2. DocketMonitor unloads old docket
3. DocketMonitor loads new docket
4. OrchestrationService logs "Docket reloaded"
5. Next request uses updated configuration
```

---

## Success Criteria

- ? **Build:** No compilation errors
- ? **Startup:** Service starts, docket loads
- ? **Health:** /health returns 200 OK
- ? **Dockets:** /dockets lists sample-receiver
- ? **Receive:** POST /receive/sample returns 202 Accepted
- ? **Transform:** Plugin adds metadata envelope
- ? **Forward:** Message sent to webhook.site
- ? **Logs:** Clear, informative log messages
- ? **Hot Reload:** File changes detected and applied
- ? **Error Handling:** Invalid paths return 404

---

## Architecture Validation

### Clean Architecture ?
- Host ? Application ? Domain
- No backward dependencies
- Proper layer separation

### Catch-All Routing ?
- Single endpoint handles all receivers
- Queries DocketManager for path matching
- No dynamic endpoint registration

### IHostedService ?
- Orchestration starts automatically
- Proper lifecycle management
- Clean shutdown

### Plugin System ?
- Plugins loaded dynamically
- Implements IReceiverPlugin
- Transform adds metadata

### Hot Reload ?
- FileSystemWatcher detects changes
- Dockets reload automatically
- No service restart needed

---

## Performance Expectations

- **Startup:** < 2 seconds
- **Request latency:** < 50ms (excluding plugin)
- **Plugin execution:** < 100ms (simple transform)
- **Total E2E:** < 200ms
- **Memory:** < 100MB at idle

---

## Known Limitations

1. **Single HTTP sender instance** - No parallelism for multiple destinations
2. **No retry tracking** - Polly retries but no visibility
3. **No endpoint unregistration** - Hot reload works but endpoints accumulate (catch-all mitigates this)
4. **Webhook URL hardcoded** - Need to update YAML manually

---

## Next Steps After E2E Test

1. **Performance testing** - Load test with multiple concurrent requests
2. **Plugin variations** - Test with different plugins (HL7, FHIR)
3. **Error scenarios** - Test plugin failures, network failures
4. **Multiple dockets** - Test with 10+ dockets
5. **Poller implementation** - Add scheduler for poller-type dockets

---

## Test Execution Commands

### Quick Test (All-in-One)

```bash
# 1. Update webhook URL in dockets/sample-receiver.yaml

# 2. Run service (Terminal 1)
dotnet run --project src/KeryxFlux.Host

# 3. Test (Terminal 2)
curl http://localhost:5000/health
curl http://localhost:5000/dockets
curl -X POST http://localhost:5000/receive/sample \
  -H "Content-Type: application/json" \
  -d '{"test": "e2e"}'

# 4. Check webhook.site
```

### PowerShell Test

```powershell
# Run service
Start-Process powershell -ArgumentList "dotnet run --project src/KeryxFlux.Host"

# Wait for startup
Start-Sleep 5

# Test
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod http://localhost:5000/dockets

$body = @{ test = "e2e" } | ConvertTo-Json
Invoke-RestMethod -Method POST -Uri http://localhost:5000/receive/sample `
  -Body $body -ContentType "application/json"
```

---

**Status:** ? **READY FOR MANUAL E2E TEST**

All automated checks passed. Manual testing required to verify:
1. Update webhook.site URL
2. Run dotnet run
3. Send test request
4. Verify on webhook.site

**The architecture is solid. Time to see it work end-to-end!** ??
