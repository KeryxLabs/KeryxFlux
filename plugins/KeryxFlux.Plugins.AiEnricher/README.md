# AI Enricher Plugin

Example plugin demonstrating model-enhanced transformations using the `IModelPlugin` interface.

## Overview

This plugin shows how to integrate AI/ML model invocations into KeryxFlux transformation pipelines. The plugin orchestrates sequential calls to model endpoints (Ollama, OpenAI, custom inference services) during message processing.

## Features

- Calls AI model endpoints via HTTP
- Sequential model invocations (can chain multiple models)
- Accumulates state across steps
- Flexible: works with any HTTP-based model API

## Configuration

```yaml
configuration:
  model_endpoint: "http://localhost:11434/api/generate"
  model: "llama3.2"
```

## How It Works

### 1. ParseInitialMessage
Plugin receives incoming data and decides what model calls are needed:

```csharp
public ModelInvocationPlan ParseInitialMessage(byte[] data, TransformationContext context)
{
    // Build prompt from incoming data
    var prompt = $"Extract entities from: {input}";
    
    // Return model step
    return ModelInvocationPlan.WithStep(new ModelStep
    {
        Name = "extract_entities",
        Endpoint = _modelEndpoint,
        RequestBody = BuildRequest(prompt)
    });
}
```

### 2. TransformModelResponse
Plugin processes model response and decides next step:

```csharp
public ModelStepResult TransformModelResponse(
    ModelStep currentStep,
    byte[] modelResponse,
    AccumulatedState state)
{
    // Parse model output
    var entities = ParseResponse(modelResponse);
    
    // Option A: Continue with validation step
    return ModelStepResult.ContinueWith(validationStep);
    
    // Option B: Complete and forward
    return ModelStepResult.Complete(finalData);
}
```

## Compatible Model Services

- **Ollama** (local LLM hosting)
- **OpenAI API** (GPT models)
- **Anthropic Claude API**
- **Custom inference endpoints**

## Building

```bash
cd plugins/KeryxFlux.Plugins.AiEnricher
dotnet build
```

Output: `bin/Debug/net8.0/KeryxFlux.Plugins.AiEnricher.dll`

## Usage Example

See `dockets/examples/ai-enricher.yaml` for complete docket configuration.

## Extending

This plugin serves as a template. Customize for your needs:

- **Multi-model workflow:** Chain warm_brain ? validator ? enricher
- **Conditional logic:** Skip validation if confidence score is high
- **Error handling:** Retry with different model if first fails
- **Response parsing:** Extract structured data from model output

## Pattern

This follows the same pattern as `IPollerPlugin`:
- Plugin orchestrates workflow
- Infrastructure handles HTTP calls
- State accumulates across steps
- Plugin decides when to complete

Simple, flexible, powerful.
