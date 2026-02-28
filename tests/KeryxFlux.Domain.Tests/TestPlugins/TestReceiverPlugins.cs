using KeryxFlux.Contracts;
using KeryxFlux.Domain.Models;

namespace KeryxFlux.Domain.Tests.TestPlugins;

/// <summary>
/// Simple test plugin that echoes the received payload.
/// Used for testing orchestration flow.
/// </summary>
public class EchoReceiverPlugin : IReceiverPlugin
{
    public string Name => "EchoReceiver";
    public string Version => "1.0.0";
    public string Description => "Test plugin that echoes received data";

    public TransformationResult Transform(byte[] data, TransformationContext context)
    {
        // Simple echo: return same data with metadata
        return TransformationResult.Success(
            data, 
            "application/octet-stream");
    }

    public void Initialize(IReadOnlyDictionary<string, string> configuration)
    {
        // No initialization needed for test plugin
    }
}

/// <summary>
/// Test plugin that appends text to payload.
/// </summary>
public class AppendTextReceiverPlugin : IReceiverPlugin
{
    public string Name => "AppendTextReceiver";
    public string Version => "1.0.0";
    public string Description => "Test plugin that appends text to payload";

    private string _appendText = " [PROCESSED]";

    public TransformationResult Transform(byte[] data, TransformationContext context)
    {
        var original = System.Text.Encoding.UTF8.GetString(data);
        var transformed = original + _appendText;
        var result = System.Text.Encoding.UTF8.GetBytes(transformed);

        return TransformationResult.Success(
            result,
            "text/plain");
    }

    public void Initialize(IReadOnlyDictionary<string, string> configuration)
    {
        if (configuration.TryGetValue("append_text", out var text))
        {
            _appendText = text;
        }
    }
}

/// <summary>
/// Test plugin that always fails (for error handling tests).
/// </summary>
public class FailingReceiverPlugin : IReceiverPlugin
{
    public string Name => "FailingReceiver";
    public string Version => "1.0.0";
    public string Description => "Test plugin that always fails";

    public TransformationResult Transform(byte[] data, TransformationContext context)
    {
        return TransformationResult.Failure("Intentional test failure");
    }

    public void Initialize(IReadOnlyDictionary<string, string> configuration)
    {
        // No initialization needed
    }
}
