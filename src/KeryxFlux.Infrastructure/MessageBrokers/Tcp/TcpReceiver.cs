using KeryxFlux.Application.Commands;
using KeryxFlux.Contracts;
using KeryxFlux.Domain.Abstractions;
using KeryxFlux.Domain.Models;
using KeryxFlux.Domain.Ports;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KeryxFlux.Infrastructure.MessageBrokers.Tcp;

/// <summary>
/// Service for managing TCP listeners for receiver dockets.
/// Supports multiple message framing strategies: MLLP, length-prefixed, delimiter-based.
/// Maintains connection pool similar to RabbitMQ pattern.
/// </summary>
public class TcpReceiver : ITcpReceiverService
{
    private readonly ILogger<TcpReceiver> _logger;
    private readonly IDocketManager _docketManager;
    private readonly IPluginManager _pluginManager;
    private readonly IMediator _mediator;
    
    // Track active listeners: docketName ? (listener, cancellationTokenSource)
    private readonly ConcurrentDictionary<string, (TcpListener Listener, CancellationTokenSource Cts)> _activeListeners = new();

    public TcpReceiver(
        ILogger<TcpReceiver> logger,
        IDocketManager docketManager,
        IPluginManager pluginManager,
        IMediator mediator)
    {
        _logger = logger;
        _docketManager = docketManager;
        _pluginManager = pluginManager;
        _mediator = mediator;
    }

    public async Task RegisterReceiverForDocketAsync(Docket docket)
    {
        if (docket.Receiver == null)
        {
            throw new InvalidOperationException($"Docket {docket.Name} has no receiver configuration");
        }

        var config = docket.Receiver;

        if (!config.Port.HasValue)
        {
            throw new InvalidOperationException($"TCP port is required for docket {docket.Name}");
        }

        var port = config.Port.Value;
        var framing = config.Framing ?? "mllp";

        _logger.LogInformation(
            "Registering TCP listener for docket: {DocketName}, port: {Port}, framing: {Framing}",
            docket.Name,
            port,
            framing);

        try
        {
            var listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            var cts = new CancellationTokenSource();
            _activeListeners[docket.Name] = (listener, cts);

            // Start accepting connections in background
            _ = Task.Run(() => AcceptConnectionsAsync(docket, listener, cts.Token), cts.Token);

            _logger.LogInformation(
                "TCP listener started for docket: {DocketName} on port {Port}",
                docket.Name,
                port);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start TCP listener for docket {DocketName} on port {Port}", 
                docket.Name, port);
            throw;
        }
    }

    public async Task UnregisterReceiverForDocketAsync(string docketName)
    {
        if (!_activeListeners.TryRemove(docketName, out var entry))
        {
            _logger.LogWarning("No active TCP listener found for docket: {DocketName}", docketName);
            return;
        }

        var (listener, cts) = entry;

        _logger.LogInformation("Stopping TCP listener for docket: {DocketName}", docketName);

        try
        {
            cts.Cancel();
            listener.Stop();
            cts.Dispose();

            _logger.LogInformation("TCP listener stopped for docket: {DocketName}", docketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping TCP listener for docket {DocketName}", docketName);
        }

        await Task.CompletedTask;
    }

    private async Task AcceptConnectionsAsync(Docket docket, TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                
                _logger.LogInformation(
                    "TCP connection accepted for docket {DocketName} from {RemoteEndpoint}",
                    docket.Name,
                    client.Client.RemoteEndPoint);

                // Handle each connection in background
                _ = Task.Run(() => HandleConnectionAsync(docket, client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting TCP connection for docket {DocketName}", docket.Name);
            }
        }
    }

    private async Task HandleConnectionAsync(Docket docket, TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var framing = docket.Receiver!.Framing ?? "mllp";
                var persistentConnection = docket.Receiver.PersistentConnection ?? true;

                do
                {
                    byte[]? message = await ReadMessageAsync(stream, framing, docket.Receiver.Delimiter, cancellationToken);

                    if (message == null)
                    {
                        // Connection closed by remote
                        break;
                    }

                    _logger.LogInformation(
                        "Received TCP message for docket {DocketName}, size: {Size} bytes",
                        docket.Name,
                        message.Length);

                    // Process message through MediatR pipeline
                    await ProcessMessageAsync(docket.Name, message, client.Client.RemoteEndPoint?.ToString() ?? "unknown", cancellationToken);

                } while (persistentConnection && !cancellationToken.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling TCP connection for docket {DocketName}", docket.Name);
            }
        }
    }

    private async Task<byte[]?> ReadMessageAsync(NetworkStream stream, string framing, string? delimiter, CancellationToken cancellationToken)
    {
        return framing.ToLowerInvariant() switch
        {
            "mllp" => await ReadMllpMessageAsync(stream, cancellationToken),
            "length_prefixed" => await ReadLengthPrefixedMessageAsync(stream, cancellationToken),
            "delimiter" => await ReadDelimiterBasedMessageAsync(stream, delimiter ?? "\n", cancellationToken),
            _ => throw new NotSupportedException($"Framing strategy '{framing}' is not supported")
        };
    }

    private async Task<byte[]?> ReadMllpMessageAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        // MLLP format: 0x0B (start) + message + 0x1C (end) + 0x0D (carriage return)
        const byte START_OF_BLOCK = 0x0B;
        const byte END_OF_BLOCK = 0x1C;
        const byte CARRIAGE_RETURN = 0x0D;

        // Read start byte
        var buffer = new byte[1];
        int read = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
        if (read == 0) return null;

        if (buffer[0] != START_OF_BLOCK)
        {
            throw new InvalidDataException($"Expected MLLP start byte 0x0B, got 0x{buffer[0]:X2}");
        }

        // Read message until end markers
        var messageBuffer = new List<byte>();
        while (true)
        {
            read = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
            if (read == 0) throw new InvalidDataException("Connection closed before MLLP end marker");

            if (buffer[0] == END_OF_BLOCK)
            {
                // Read carriage return
                read = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
                if (read == 0 || buffer[0] != CARRIAGE_RETURN)
                {
                    throw new InvalidDataException("Expected MLLP carriage return after end block");
                }
                break;
            }

            messageBuffer.Add(buffer[0]);
        }

        return messageBuffer.ToArray();
    }

    private async Task<byte[]?> ReadLengthPrefixedMessageAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        // Read 4-byte length header (big-endian)
        var lengthBuffer = new byte[4];
        int totalRead = 0;
        while (totalRead < 4)
        {
            int read = await stream.ReadAsync(lengthBuffer, totalRead, 4 - totalRead, cancellationToken);
            if (read == 0) return null;
            totalRead += read;
        }

        // Convert to int (big-endian)
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBuffer);
        }
        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

        if (messageLength < 0 || messageLength > 10_000_000) // 10MB limit
        {
            throw new InvalidDataException($"Invalid message length: {messageLength}");
        }

        // Read message
        var messageBuffer = new byte[messageLength];
        totalRead = 0;
        while (totalRead < messageLength)
        {
            int read = await stream.ReadAsync(messageBuffer, totalRead, messageLength - totalRead, cancellationToken);
            if (read == 0) throw new InvalidDataException("Connection closed before message complete");
            totalRead += read;
        }

        return messageBuffer;
    }

    private async Task<byte[]?> ReadDelimiterBasedMessageAsync(NetworkStream stream, string delimiter, CancellationToken cancellationToken)
    {
        var delimiterBytes = Encoding.UTF8.GetBytes(delimiter);
        var messageBuffer = new List<byte>();
        var matchBuffer = new List<byte>();

        while (true)
        {
            var buffer = new byte[1];
            int read = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
            if (read == 0) return messageBuffer.Count > 0 ? messageBuffer.ToArray() : null;

            matchBuffer.Add(buffer[0]);

            // Check if we have delimiter match
            if (matchBuffer.Count >= delimiterBytes.Length)
            {
                var lastBytes = matchBuffer.Skip(matchBuffer.Count - delimiterBytes.Length).ToArray();
                if (lastBytes.SequenceEqual(delimiterBytes))
                {
                    // Found delimiter - return message without delimiter
                    var messageWithoutDelimiter = messageBuffer.Take(messageBuffer.Count).ToArray();
                    return messageWithoutDelimiter;
                }
            }

            messageBuffer.Add(buffer[0]);
        }
    }

    private async Task ProcessMessageAsync(string docketName, byte[] payload, string remoteEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            var command = new ProcessMessageCommand
            {
                DocketName = docketName,
                Message = new ReceivedMessage
                {
                    Payload = payload,
                    ContentType = "application/octet-stream",
                    CorrelationId = Guid.NewGuid().ToString(),
                    SourceEndpoint = remoteEndpoint,
                    Metadata = new Dictionary<string, string>
                    {
                        { "source", "tcp" },
                        { "remote_endpoint", remoteEndpoint }
                    }
                }
            };

            var result = await _mediator.Send(command, cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Successfully processed TCP message for docket {DocketName}. Forwarded to {Count} destinations.",
                    docketName,
                    result.DestinationsForwarded);
            }
            else
            {
                _logger.LogError(
                    "Failed to process TCP message for docket {DocketName}: {Error}",
                    docketName,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing TCP message for docket: {DocketName}", docketName);
        }
    }
}
