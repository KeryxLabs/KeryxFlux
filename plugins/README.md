# KeryxFlux Plugins

This directory contains example plugin implementations demonstrating the KeryxFlux plugin system.

## Available Plugins

### AI Enricher (`KeryxFlux.Plugins.AiEnricher/`)
Demonstrates `IModelPlugin` interface for AI/ML model integration.
- Calls model endpoints (Ollama, OpenAI, etc.)
- Sequential model invocations
- State accumulation across steps

See: [AiEnricher README](./KeryxFlux.Plugins.AiEnricher/README.md)

## Plugin Interfaces

KeryxFlux supports three plugin types:

### IReceiverPlugin
For standard transformations on received messages.

```csharp
public interface IReceiverPlugin : IKeryxFluxPlugin
{
    TransformationResult Transform(byte[] data, TransformationContext context);
}
```

**Use when:** Simple transformation (parse JSON, validate schema, enrich data)

### IPollerPlugin
For multi-step workflows with HTTP requests.

```csharp
public interface IPollerPlugin : IKeryxFluxPlugin
{
    InitialPollingResult ParseInitialResponse(byte[] source, TransformationContext context);
    StepTransformationResult TransformItemStep(NextStep currentStep, byte[] stepResponse, AccumulatedState itemState, string itemId);
}
```

**Use when:** Need to make additional HTTP calls during transformation (paginated APIs, multi-step workflows)

### IModelPlugin
For model-enhanced transformations.

```csharp
public interface IModelPlugin : IKeryxFluxPlugin
{
    ModelInvocationPlan ParseInitialMessage(byte[] data, TransformationContext context);
    ModelStepResult TransformModelResponse(ModelStep currentStep, byte[] modelResponse, AccumulatedState state);
}
```

**Use when:** Calling AI/ML models during transformation (enrichment, classification, generation)

## Building Plugins

1. Create new class library project:
```bash
dotnet new classlib -n MyPlugin -f net8.0
```

2. Add KeryxFlux references:
```bash
dotnet add reference ../../src/KeryxFlux.Contracts/KeryxFlux.Contracts.csproj
dotnet add reference ../../src/KeryxFlux.Domain/KeryxFlux.Domain.csproj
```

3. Implement interface:
```csharp
public class MyPlugin : IReceiverPlugin
{
    public string Name => "MyPlugin";
    public string Version => "1.0.0";
    public string Description => "My custom transformation";

    public void Initialize(IReadOnlyDictionary<string, string> configuration)
    {
        // Load configuration
    }

    public TransformationResult Transform(byte[] data, TransformationContext context)
    {
        // Transform data
        return TransformationResult.Success(transformedData, "application/json");
    }
}
```

4. Build:
```bash
dotnet build
```

5. Reference in docket:
```yaml
plugin_location: ./plugins/MyPlugin.dll
```

## Plugin Development Tips

- Keep plugins stateless (use `AccumulatedState` for workflow state)
- Return errors via `TransformationResult.Failure()` instead of throwing
- Use `context.DocketConfiguration` for runtime config values
- Log important steps (plugins receive no logger, rely on orchestrator logging)
- Test plugins independently before integration

## Documentation

- [Plugin Development Guide](../README.md#plugins)
- [Example Dockets](../dockets/examples/)
- [Architecture Overview](../README.md#architecture)
