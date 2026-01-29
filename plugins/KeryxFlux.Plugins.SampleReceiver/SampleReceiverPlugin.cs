using System.Text;
using System.Text.Json;
using KeryxFlux.Contracts;

namespace KeryxFlux.Plugins.SampleReceiver;

/// <summary>
/// Simple receiver plugin that adds a metadata envelope to incoming JSON.
/// Perfect for testing the HTTP receiver flow.
/// </summary>
public class SampleReceiverPlugin : IReceiverPlugin
{
    public string Name => "SampleReceiverPlugin";
    public string Version => "1.0.0";

    public TransformationResult Transform(byte[] source, TransformationContext context)
    {
        try
        {
            var input = Encoding.UTF8.GetString(source);

            // Add metadata envelope
            var envelope = new
            {
                metadata = new
                {
                    receivedAt = context.ReceivedAt,
                    docket = context.DocketName,
                    correlationId = context.CorrelationId,
                    source = context.ReceiverType,
                    pluginName = Name,
                    pluginVersion = Version
                },
                originalPayload = JsonDocument.Parse(input).RootElement
            };

            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return TransformationResult.Success(
                Encoding.UTF8.GetBytes(json),
                "application/json"
            );
        }
        catch (JsonException jsonEx)
        {
            return TransformationResult.Failure($"Invalid JSON: {jsonEx.Message}");
        }
        catch (Exception ex)
        {
            return TransformationResult.Failure($"Transformation failed: {ex.Message}");
        }
    }
}
