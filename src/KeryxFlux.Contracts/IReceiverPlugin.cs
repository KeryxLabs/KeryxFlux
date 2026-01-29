namespace KeryxFlux.Contracts;

/// <summary>
/// Plugin interface for receiver-type dockets (HTTP, TCP, RabbitMQ, Kafka).
/// Receivers process a single incoming message and transform it.
/// This is a single-step, synchronous transformation.
/// </summary>
public interface IReceiverPlugin : IKeryxFluxPlugin
{
    /// <summary>
    /// Transform incoming data from a receiver.
    /// </summary>
    /// <param name="source">Raw bytes from the receiver (HTTP body, message queue payload, etc.)</param>
    /// <param name="context">Execution context with metadata about the request</param>
    /// <returns>Result containing transformed data or error information</returns>
    TransformationResult Transform(byte[] source, TransformationContext context);
}
