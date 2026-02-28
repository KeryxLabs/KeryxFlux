using KeryxFlux.Contracts;
using KeryxFlux.Domain.Models;
using System.Text;
using System.Text.Json;

namespace KeryxFlux.Plugins.AiEnricher;

/// <summary>
/// Example plugin demonstrating model-enhanced transformations.
/// Calls an AI model endpoint to enrich incoming data.
/// </summary>
public class AiEnricherPlugin : IModelPlugin
{
    private string _modelEndpoint = "http://localhost:11434/api/generate";
    private string _modelName = "llama3.2";

    public string Name => "AiEnricher";
    public string Version => "1.0.0";
    public string Description => "Enriches data using AI model invocations";

    public void Initialize(IReadOnlyDictionary<string, string> configuration)
    {
        if (configuration.TryGetValue("model_endpoint", out var endpoint))
        {
            _modelEndpoint = endpoint;
        }

        if (configuration.TryGetValue("model", out var model))
        {
            _modelName = model;
        }
    }

    public ModelInvocationPlan ParseInitialMessage(byte[] data, TransformationContext context)
    {
        // Extract input text
        var inputText = Encoding.UTF8.GetString(data);

        // Build prompt for model
        var systemPrompt = "Extract key entities and information from the text.";
        var userPrompt = $"Text to analyze: {inputText}";
        var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";

        // Build request for Ollama API format
        var requestBody = JsonSerializer.SerializeToUtf8Bytes(new
        {
            model = _modelName,
            prompt = fullPrompt,
            stream = false
        });

        // Return plan with first model step
        return ModelInvocationPlan.WithStep(new ModelStep
        {
            Name = "extract_entities",
            Endpoint = _modelEndpoint,
            Method = "POST",
            RequestBody = requestBody,
            Headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            }
        });
    }

    public ModelStepResult TransformModelResponse(
        ModelStep currentStep,
        byte[] modelResponse,
        AccumulatedState state)
    {
        // Parse model response (Ollama format)
        var responseText = Encoding.UTF8.GetString(modelResponse);
        using var doc = JsonDocument.Parse(responseText);
        
        var modelOutput = doc.RootElement.GetProperty("response").GetString() ?? "";

        // Store model output in state
        state.Add("model_output", Encoding.UTF8.GetBytes(modelOutput));
        state.Add("model_step", currentStep.Name);

        // For this example, we're done after one model call
        // In production, could add validation step, enrichment step, etc.
        
        // Create final enriched output
        var finalOutput = JsonSerializer.SerializeToUtf8Bytes(new
        {
            original_input = state.Get<byte[]>("original_input"),
            extracted_entities = modelOutput,
            processed_at = DateTimeOffset.UtcNow,
            model_used = _modelName
        });

        return ModelStepResult.Complete(finalOutput, "application/json");
    }
}
