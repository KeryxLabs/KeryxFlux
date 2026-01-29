# Testing Guide (Clean Architecture)

## ?? Overview

Now that the architecture is clean, here's how to properly test the system.

---

## ?? Project Structure

```
KeryxFlux/
??? src/KeryxFlux.Host/          # Production host (clean)
??? examples/
?   ??? dockets/                  # Example YAML configurations
?   ??? scripts/                  # Test scripts
??? dockets/                      # Your runtime dockets (gitignored)
```

---

## ?? Quick Start

### Step 1: Create Runtime Dockets Directory

```bash
mkdir dockets
```

### Step 2: Copy Example Docket

```bash
cp examples/dockets/sample-receiver.yaml dockets/
```

### Step 3: Update Configuration

Edit `dockets/sample-receiver.yaml` and update the webhook URL:

```yaml
forwarding:
  destinations:
    - name: webhook-site
      type: http
      url: https://webhook.site/YOUR-UNIQUE-ID  # Get from webhook.site
```

### Step 4: Build and Run

```bash
dotnet build
dotnet run --project src/KeryxFlux.Host
```

---

## ?? Testing Workflow

### 1. Service Should Start Clean

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started.
```

**Note:** No dockets are loaded yet (we'll implement YAML loading next)

### 2. Health Check

```bash
curl http://localhost:5000/health
```

Expected:
```json
{
  "status": "healthy",
  "timestamp": "2024-01-29T..."
}
```

### 3. Check Loaded Dockets

```bash
curl http://localhost:5000/dockets
```

Expected (for now):
```json
{
  "receivers": [],
  "pollers": []
}
```

---

## ?? Next Phase: YAML Docket Loading

To make the example dockets actually work, we need to implement:

### Phase 1.5: YAML Configuration Loader

**Goal:** Load dockets from `dockets/*.yaml` on startup

**Implementation:**
1. Create `DocketLoader` service
2. Read YAML files from `dockets/` directory
3. Parse using existing `Loader.LoadDocket()`
4. Register endpoints dynamically for receivers
5. Schedule pollers

**Then you can:**
```bash
# Just drop a YAML file
cp examples/dockets/sample-receiver.yaml dockets/

# Restart host
dotnet run --project src/KeryxFlux.Host

# Endpoint automatically available!
curl -X POST http://localhost:5000/receive/sample -d '{"test": "data"}'
```

---

## ?? Example Docket Anatomy

```yaml
# Docket identity
name: sample-receiver
version: 1.0.0
type: Receiver

# Plugin to use
plugin:
  location: plugins/KeryxFlux.Plugins.SampleReceiver.dll

# How to receive data
receiver:
  type: http
  endpoint: /receive/sample

# Where to forward transformed data
forwarding:
  destinations:
    - name: webhook-site
      type: http
      url: https://webhook.site/...
      timeout_seconds: 30
```

---

## ??? Clean Architecture Benefits

### Before (Rushed Implementation)

```csharp
// Program.cs - 200+ lines
public class InMemoryDocketManager { ... }  // ? Business logic in Host
public class SimplePluginManager { ... }     // ? Duplicate implementation
```

**Problems:**
- Can't load dockets from files
- Hard to test
- Violates separation of concerns
- Configuration baked into code

### After (Clean Implementation)

```csharp
// Program.cs - 50 lines
builder.Services.AddSingleton<IDocketManager, DocketManager>();
builder.Services.AddSingleton<IPluginManager, PluginManager>();
```

**Benefits:**
- ? Can load from YAML files
- ? Easy to test (mock IDocketManager)
- ? Follows clean architecture
- ? Configuration external to code

---

## ?? Testing Strategy

### Unit Tests

```csharp
public class PluginManagerTests
{
    [Fact]
    public void LoadPlugin_ValidPath_ReturnsSuccess()
    {
        var manager = new PluginManager(logger);
        var result = manager.LoadPlugin("path/to/plugin.dll");
        
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }
}
```

### Integration Tests

```csharp
public class DocketLoadingTests
{
    [Fact]
    public async Task LoadDocket_ValidYaml_RegistersEndpoint()
    {
        // Arrange
        var host = CreateHost();
        var docketPath = new DocketPath("dockets/sample.yaml");
        
        // Act
        var loaded = docketManager.TryLoad(docketPath, out var docket);
        
        // Assert
        Assert.True(loaded);
        Assert.Equal("sample-receiver", docket.Name);
    }
}
```

### Manual/E2E Tests

```bash
# Use the provided test scripts
examples/scripts/test-receiver.ps1
examples/scripts/test-receiver.sh
```

---

## ?? Architectural Verification

### ? Clean Architecture Checklist

- ? Host has no business logic
- ? Services in Application layer
- ? Domain independent of infrastructure
- ? Dependencies point inward
- ? Testable in isolation

### ? Separation of Concerns

| Layer | Responsibility | Dependencies |
|-------|---------------|--------------|
| Host | DI, routing | Application, Infrastructure |
| Application | Services, handlers | Domain |
| Domain | Business logic | None (pure) |
| Infrastructure | I/O, external | Domain ports |

### ? Examples vs Production

| Aspect | Examples | Production |
|--------|----------|------------|
| Location | `examples/` | `src/` |
| Purpose | Learning, demos | Running system |
| Configuration | Sample YAML | Runtime YAML |
| Plugins | Sample plugin | Real transformations |

---

## ?? What's Next

### Immediate (Phase 1.5)

1. **YAML Docket Loader**
   - Read from `dockets/*.yaml`
   - Validate docket configuration
   - Load plugins
   - Register endpoints dynamically

2. **Hot Reload**
   - Watch `dockets/` directory
   - Reload on file changes
   - Graceful unload/reload

### Future (Phase 2)

1. **More Plugins**
   - HL7 v2 ? FHIR
   - FHIR ? HL7 v2
   - Custom validators

2. **Poller Implementation**
   - Quartz.NET scheduler
   - Multi-step workflows
   - Item-level parallelism

3. **Production Features**
   - Authentication
   - Rate limiting
   - Metrics & monitoring

---

## ?? Key Takeaways

### Architecture Matters

Rushing led to violations. Taking time to follow principles results in:
- ? Cleaner code
- ? Easier maintenance
- ? Better testability
- ? Proper extensibility

### Examples Are Separate

Demo code doesn't belong in production:
- ? `examples/` for learning
- ? `src/` for production
- ? Clear boundaries

### Use What Exists

Before creating new implementations:
- ? Check existing services
- ? Use existing infrastructure
- ? Extend rather than duplicate

---

**Status:** ? **ARCHITECTURE CLEAN**  
**Next:** Implement YAML docket loading to make examples work

The foundation is solid. Now let's build on it properly! ??
