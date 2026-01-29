# Context-Specific Plugin Interfaces: Design Update

## ?? Problem Solved

The original design had a single `IKeryxFluxPlugin` interface with a `Transform()` method, and an optional `IMultiStepKeryxFluxPlugin` for advanced scenarios. This had issues:

? **Unclear intent** - Plugin type doesn't indicate its purpose  
? **Wrong abstraction** - Receivers don't need multi-step logic  
? **Optional behavior** - Multi-step was an add-on, not built-in where needed  
? **Type confusion** - Easy to use wrong plugin in wrong context

---

## ? New Design: Context-Specific Interfaces

```
IKeryxFluxPlugin (base abstraction)
??? IReceiverPlugin (single-step, for incoming messages)
??? IPollerPlugin (multi-step by default, for API polling)
```

### Benefits

? **Clear intent** - Interface name tells you its purpose  
? **Type safety** - Can't use receiver plugin in poller context  
? **Simpler API** - Each interface has only what it needs  
? **Built-in behavior** - Pollers support multi-step by default  
? **Better DI** - Can register different plugin types separately

---

## ?? Interface Definitions

### Base Interface

```csharp
public interface IKeryxFluxPlugin
{
    string Name { get; }
    string Version { get; }
}
```

### Receiver Plugin (Single-Step)

```csharp
public interface IReceiverPlugin : IKeryxFluxPlugin
{
    TransformationResult Transform(byte[] source, TransformationContext context);
}
```

**Used for:**
- HTTP receivers (incoming POST requests)
- TCP/MLLP receivers (HL7 messages)
- RabbitMQ/Kafka consumers
- Any scenario where you get ONE message and transform it

**Characteristics:**
- Stateless
- Synchronous transformation
- Returns `TransformationResult`

### Poller Plugin (Multi-Step)

```csharp
public interface IPollerPlugin : IKeryxFluxPlugin
{
    StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState accumulatedState);
}
```

**Used for:**
- Scheduled API polling
- Paginated API responses
- Chained API calls (list ? details for each)
- Any scenario requiring multiple HTTP requests

**Characteristics:**
- Stateful (`AccumulatedState` passed through steps)
- Supports multi-step workflows
- Returns `StepTransformationResult` with continuation or completion

---

## ?? Migration from Old Design

### Old Way (Deprecated)

```csharp
// Old: Single interface with optional multi-step
public class MyPlugin : IKeryxFluxPlugin, IMultiStepKeryxFluxPlugin
{
    public TransformationResult Transform(...) { }  // For receivers
    public StepTransformationResult TransformStep(...) { }  // For pollers
}
```

### New Way

```csharp
// Receiver plugin (single-step)
public class HttpReceiverPlugin : IReceiverPlugin
{
    public TransformationResult Transform(byte[] source, TransformationContext context)
    {
        // Transform incoming HTTP message
        return TransformationResult.Success(transformedData, "application/json");
    }
}

// Poller plugin (multi-step capable)
public class ApiPollerPlugin : IPollerPlugin
{
    public StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState state)
    {
        // Even single-step pollers use this interface
        // Just return Complete() immediately for single-step
        return StepTransformationResult.Complete(finalData, "application/json", state);
    }
}
```

---

## ?? Comparison

| Aspect | IReceiverPlugin | IPollerPlugin |
|--------|----------------|---------------|
| **Method** | `Transform()` | `TransformStep()` |
| **State** | None (stateless) | `AccumulatedState` |
| **Steps** | Always 1 | 1 or many |
| **Result** | `TransformationResult` | `StepTransformationResult` |
| **Use Case** | Incoming messages | API polling |
| **Complexity** | Simple | Can be complex |
| **Example** | HL7 ? JSON | Epic FHIR patient sync |

---

## ??? Handler Changes

### ProcessMessageCommandHandler (for Receivers)

```csharp
// Old: Checked for IKeryxFluxPlugin
if (plugin is not IKeryxFluxPlugin) { ... }

// New: Checks for IReceiverPlugin
if (plugin is not IReceiverPlugin receiverPlugin)
{
    return ProcessMessageResult.Failure(
        "Plugin must implement IReceiverPlugin for receiver-type dockets");
}

var result = receiverPlugin.Transform(message.Payload, context);
```

### ProcessMultiStepCommandHandler (for Pollers)

```csharp
// Old: Checked for IMultiStepKeryxFluxPlugin with fallback
if (plugin is not IMultiStepKeryxFluxPlugin multiStepPlugin)
{
    // Fallback to single-step
}

// New: Checks for IPollerPlugin (no fallback needed)
if (plugin is not IPollerPlugin pollerPlugin)
{
    return ProcessMessageResult.Failure(
        "Plugin must implement IPollerPlugin for poller-type dockets");
}

var result = pollerPlugin.TransformStep(message.Payload, context, state);
```

---

## ?? Real-World Examples

### Example 1: Simple HTTP Receiver Plugin

```csharp
public class JsonEnvelopePlugin : IReceiverPlugin
{
    public string Name => "JsonEnvelopePlugin";
    public string Version => "1.0.0";

    public TransformationResult Transform(byte[] source, TransformationContext context)
    {
        var input = Encoding.UTF8.GetString(source);
        
        var envelope = new
        {
            metadata = new { receivedAt = context.ReceivedAt, correlationId = context.CorrelationId },
            payload = JsonDocument.Parse(input).RootElement
        };

        return TransformationResult.Success(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope)),
            "application/json"
        );
    }
}
```

### Example 2: Single-Step Poller Plugin

```csharp
public class SimpleApiPollerPlugin : IPollerPlugin
{
    public string Name => "SimpleApiPollerPlugin";
    public string Version => "1.0.0";

    public StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState state)
    {
        // Parse and transform immediately (single-step)
        var data = JsonSerializer.Deserialize<ApiResponse>(Encoding.UTF8.GetString(source));
        var output = JsonSerializer.Serialize(data);

        // Return Complete (not ContinueWithSteps)
        return StepTransformationResult.Complete(
            Encoding.UTF8.GetBytes(output),
            "application/json",
            state
        );
    }
}
```

### Example 3: Multi-Step Poller Plugin

```csharp
public class EpicPatientSyncPlugin : IPollerPlugin
{
    public string Name => "EpicPatientSyncPlugin";
    public string Version => "1.0.0";

    public StepTransformationResult TransformStep(
        byte[] source,
        TransformationContext context,
        AccumulatedState state)
    {
        var stepName = context.Metadata.GetValueOrDefault("stepName", "initial");

        return stepName switch
        {
            "initial" => HandlePatientList(source, state),
            "details" => HandlePatientDetails(source, context, state),
            _ => StepTransformationResult.Failure($"Unknown step: {stepName}", state)
        };
    }

    private StepTransformationResult HandlePatientList(byte[] source, AccumulatedState state)
    {
        var patients = ParsePatients(source);
        state.Add("patients", patients);

        // Create next steps for each patient
        var detailSteps = patients.Select(p => new NextStep
        {
            Name = $"details-{p.Id}",
            RequestUrl = $"/Patient/{p.Id}",
            Metadata = new Dictionary<string, string> { { "stepName", "details" } }
        }).ToList();

        return StepTransformationResult.ContinueWithSteps(detailSteps, state);
    }

    private StepTransformationResult HandlePatientDetails(
        byte[] source,
        TransformationContext context,
        AccumulatedState state)
    {
        // Accumulate details...
        // When all collected, return Complete()
        return StepTransformationResult.Complete(finalBundle, "application/json", state);
    }
}
```

---

## ?? Testing

### Testing Receiver Plugin

```csharp
[Fact]
public void ReceiverPlugin_Transform_AddsEnvelope()
{
    var plugin = new JsonEnvelopePlugin();
    var source = Encoding.UTF8.GetBytes("{\"test\": \"data\"}");
    var context = TransformationContext.Create("test-docket", "http", "test-123");

    var result = plugin.Transform(source, context);

    Assert.True(result.IsSuccess);
    var json = Encoding.UTF8.GetString(result.Data!);
    Assert.Contains("metadata", json);
    Assert.Contains("payload", json);
}
```

### Testing Poller Plugin (Single-Step)

```csharp
[Fact]
public void PollerPlugin_SingleStep_ReturnsComplete()
{
    var plugin = new SimpleApiPollerPlugin();
    var source = Encoding.UTF8.GetBytes("{\"items\": [1, 2, 3]}");
    var context = TransformationContext.Create("test-docket", "poller", "test-123");
    var state = new AccumulatedState();

    var result = plugin.TransformStep(source, context, state);

    Assert.True(result.IsSuccess);
    Assert.False(result.HasMoreSteps);
    Assert.NotNull(result.FinalData);
}
```

### Testing Poller Plugin (Multi-Step)

```csharp
[Fact]
public void PollerPlugin_MultiStep_ReturnsNextSteps()
{
    var plugin = new EpicPatientSyncPlugin();
    var source = Encoding.UTF8.GetBytes("{\"entry\": [{\"id\": \"p1\"}, {\"id\": \"p2\"}]}");
    var context = TransformationContext.Create("test-docket", "poller", "test-123");
    var state = new AccumulatedState();

    var result = plugin.TransformStep(source, context, state);

    Assert.True(result.IsSuccess);
    Assert.True(result.HasMoreSteps);
    Assert.Equal(2, result.NextSteps.Count);
}
```

---

## ?? Files Changed

### Created
- `src/KeryxFlux.Contracts/IReceiverPlugin.cs` - New receiver interface
- `docs/PLUGIN_DEVELOPMENT_GUIDE.md` - Comprehensive plugin guide

### Modified
- `src/KeryxFlux.Contracts/IKeryxFluxPlugin.cs` - Now base interface only
- `src/KeryxFlux.Contracts/IMultiStepKeryxFluxPlugin.cs` - Renamed to `IPollerPlugin`
- `src/KeryxFlux.Application/Handlers/ProcessMessageCommandHandler.cs` - Uses `IReceiverPlugin`
- `src/KeryxFlux.Application/Handlers/ProcessMultiStepCommandHandler.cs` - Uses `IPollerPlugin`

---

## ? Build Status

? **Build: SUCCESS**  
? **Zero errors**  
? **Zero warnings**  
? **All contracts defined**  
? **Handlers updated**  
? **Documentation complete**

---

## ?? Next Steps

### For Developers Using KeryxFlux

1. **Receiver Plugins**: Implement `IReceiverPlugin` for HTTP/TCP/RabbitMQ receivers
2. **Poller Plugins**: Implement `IPollerPlugin` for scheduled API polling
3. **Reference**: See `docs/PLUGIN_DEVELOPMENT_GUIDE.md` for detailed examples

### For Phase 1 Implementation

1. Update PHASE_1_GUIDE.md to use `IReceiverPlugin` for sample plugin
2. Create actual working examples of both plugin types
3. Add integration tests

---

## ?? Design Rationale

### Why Context-Specific Interfaces?

1. **Semantic Clarity**: Interface name tells you the purpose
2. **Type Safety**: Compiler prevents using wrong plugin type
3. **API Simplicity**: Each interface has only what it needs
4. **Default Behavior**: Pollers get multi-step by default
5. **Extensibility**: Easy to add `ISenderPlugin`, `ITransformerPlugin` later

### Why Multi-Step is Poller-Only?

- **Receivers** get one message and transform it (stateless)
- **Pollers** often need to make multiple API calls (stateful)
- Separating concerns makes each simpler

### Future Extensions

Could add:
- `ISenderPlugin` - Transform before sending
- `IValidatorPlugin` - Validate incoming data
- `IEnricherPlugin` - Enrich data with external lookups

---

**Date**: 2026-01-29  
**Status**: ? IMPLEMENTED  
**Build**: ? SUCCESS  
**Ready**: Phase 1 implementation
