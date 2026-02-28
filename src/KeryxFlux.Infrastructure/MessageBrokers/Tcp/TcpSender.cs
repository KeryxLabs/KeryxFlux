using KeryxFlux.Domain.Ports;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace KeryxFlux.Infrastructure.MessageBrokers.Tcp;

/// <summary>
/// TCP sender implementation.
/// Supports connection pooling for persistent connections and message framing.
/// </summary>
public class TcpSender : ISender
{
    private readonly ILogger<TcpSender> _logger;
    
    // Connection pool: "host:port" ? TcpClient
    private readonly ConcurrentDictionary<string, TcpClient> _connectionPool = new();

    public string Type => "tcp";

    public TcpSender(ILogger<TcpSender> logger)
    {
        _logger = logger;
    }

    public async Task<SendResult> SendAsync(OutboundMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract TCP configuration from headers
            if (!message.Headers.TryGetValue("host", out var host))
            {
                return SendResult.Failure("TCP host is required");
            }

            if (!message.Headers.TryGetValue("port", out var portStr) || !int.TryParse(portStr, out var port))
            {
                return SendResult.Failure("TCP port is required and must be an integer");
            }

            var framing = message.Headers.GetValueOrDefault("framing", "mllp");
            var persistent = message.Headers.GetValueOrDefault("persistent", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            var delimiter = message.Headers.GetValueOrDefault("delimiter", "\n");

            _logger.LogInformation(
                "Sending TCP message to {Host}:{Port}, framing: {Framing}, size: {Size} bytes",
                host,
                port,
                framing,
                message.Payload.Length);

            TcpClient client;
            if (persistent)
            {
                // Use pooled connection
                var connectionKey = $"{host}:{port}";
                client = _connectionPool.GetOrAdd(connectionKey, _ =>
                {
                    var newClient = new TcpClient();
                    newClient.ConnectAsync(host, port).Wait(cancellationToken);
                    _logger.LogInformation("Opened persistent TCP connection to {Host}:{Port}", host, port);
                    return newClient;
                });

                // Check if connection is still alive
                if (!client.Connected)
                {
                    _connectionPool.TryRemove(connectionKey, out _);
                    client.Dispose();
                    
                    client = new TcpClient();
                    await client.ConnectAsync(host, port, cancellationToken);
                    _connectionPool[connectionKey] = client;
                    _logger.LogInformation("Reconnected to {Host}:{Port}", host, port);
                }
            }
            else
            {
                // One-time connection
                client = new TcpClient();
                await client.ConnectAsync(host, port, cancellationToken);
            }

            var stream = client.GetStream();

            // Frame and send message
            byte[] framedMessage = FrameMessage(message.Payload, framing, delimiter);
            await stream.WriteAsync(framedMessage, 0, framedMessage.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully sent TCP message to {Host}:{Port}",
                host,
                port);

            // Close connection if not persistent
            if (!persistent)
            {
                client.Close();
                client.Dispose();
            }

            return SendResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send TCP message to {Destination}", message.DestinationName);
            return SendResult.Failure($"TCP send failed: {ex.Message}");
        }
    }

    private byte[] FrameMessage(byte[] payload, string framing, string delimiter)
    {
        return framing.ToLowerInvariant() switch
        {
            "mllp" => FrameMllp(payload),
            "length_prefixed" => FrameLengthPrefixed(payload),
            "delimiter" => FrameDelimiter(payload, delimiter),
            _ => throw new NotSupportedException($"Framing strategy '{framing}' is not supported")
        };
    }

    private byte[] FrameMllp(byte[] payload)
    {
        // MLLP format: 0x0B + message + 0x1C + 0x0D
        const byte START_OF_BLOCK = 0x0B;
        const byte END_OF_BLOCK = 0x1C;
        const byte CARRIAGE_RETURN = 0x0D;

        var framed = new byte[payload.Length + 3];
        framed[0] = START_OF_BLOCK;
        Array.Copy(payload, 0, framed, 1, payload.Length);
        framed[framed.Length - 2] = END_OF_BLOCK;
        framed[framed.Length - 1] = CARRIAGE_RETURN;

        return framed;
    }

    private byte[] FrameLengthPrefixed(byte[] payload)
    {
        // 4-byte length header (big-endian) + message
        var lengthBytes = BitConverter.GetBytes(payload.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }

        var framed = new byte[4 + payload.Length];
        Array.Copy(lengthBytes, 0, framed, 0, 4);
        Array.Copy(payload, 0, framed, 4, payload.Length);

        return framed;
    }

    private byte[] FrameDelimiter(byte[] payload, string delimiter)
    {
        // message + delimiter
        var delimiterBytes = Encoding.UTF8.GetBytes(delimiter);
        var framed = new byte[payload.Length + delimiterBytes.Length];
        Array.Copy(payload, 0, framed, 0, payload.Length);
        Array.Copy(delimiterBytes, 0, framed, payload.Length, delimiterBytes.Length);

        return framed;
    }

    public void Dispose()
    {
        // Close all pooled connections
        foreach (var connection in _connectionPool.Values)
        {
            try
            {
                connection.Close();
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing TCP connection");
            }
        }

        _connectionPool.Clear();
    }
}
