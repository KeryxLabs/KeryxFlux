namespace KeryxFlux.Domain.Models.Dockets;

/// <summary>
/// Type of docket workflow
/// </summary>
public enum DocketType
{
    /// <summary>
    /// Receives inbound data from external sources (HTTP, RabbitMQ, Kafka, TCP)
    /// </summary>
    Receiver = 0,

    /// <summary>
    /// Polls external sources on a schedule (SFTP, HTTP API, Database)
    /// </summary>
    Poller = 1
}
