using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Friflo.TmGui;

// ReSharper disable InconsistentNaming
namespace TmGui.SignalingServer;


public record SignalingMessage(string type, string? sdpOrCandidate, string? host)
{
    public override string ToString() => $"{type} > host: {host}";
}

public class GuiHost
{
    internal readonly string           name;
    internal readonly SignalingClient  client;
    internal          SignalingClient? connectedClient; // The client currently connected to this host
    
    internal GuiHost(SignalingClient client, string name) {
        this.name   = name;
        this.client = client;
    }
}

internal partial class SignalingClient
{
    private readonly    WebSocket   webSocket;
    internal            GuiHost?    host;   // the GUI this client wants to connect to
    

    internal SignalingClient(WebSocket webSocket)
    {
        this.webSocket = webSocket;
    }

    internal async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (webSocket.State == WebSocketState.Open)
        {
            await webSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
        }
    }
}


/// <summary> Single <see cref="Signaling"/> instance is shared by all sessions. </summary>
public class Signaling
{
    internal readonly ConcurrentDictionary<string, GuiHost> hosts = [];
    
    public Signaling(TmGuiBackend backend)
    {
    }
    
    private static SignalingMessage? Parse(ReadOnlySpan<byte> buffer)
    {
        try {
            return JsonSerializer.Deserialize<SignalingMessage>(buffer);
        }
        catch (JsonException) {
            return null;
        }
    }
    
    internal void ProcessMessage(SignalingClient client, ReadOnlySpan<byte> buffer)
    {
        var message = Parse(buffer);
        if (message == null) {
            return;
        }

        switch (message.type)
        {
            case "register":
                if (!string.IsNullOrEmpty(message.host)) {
                    // Safe upsert to prevent duplicate key crashes
                    hosts[message.host] = new GuiHost(client, message.host);
                }
                break;
                
            case "connect":
                if (!string.IsNullOrEmpty(message.host)) {
                    if (hosts.TryGetValue(message.host, out var host)) {
                        client.host = host;
                        host.connectedClient = client; // Link the client to this host session
                        
                        // Report success back to the client
                        var successResponse = JsonSerializer.SerializeToUtf8Bytes(new SignalingMessage("ConnectResponse", "Found", message.host));
                        _ = client.SendAsync(successResponse);
                    }
                    else {
                        // Report that the host was not found
                        var errorResponse = JsonSerializer.SerializeToUtf8Bytes(new SignalingMessage("ConnectResponse", "NotFound", message.host));
                        _ = client.SendAsync(errorResponse);
                    }
                }
                break;

            case "offer":
            case "answer":
            case "candidate":
                RouteWebRtcMessage(client, message, buffer);
                break;
        }
    }

    private void RouteWebRtcMessage(SignalingClient sender, SignalingMessage message, ReadOnlySpan<byte> buffer)
    {
        // Case 1: Sender is a Client forwarding an offer or candidate to the Host
        if (sender.host != null && sender != sender.host.client)
        {
            _ = sender.host.client.SendAsync(buffer.ToArray());
        }
        // Case 2: Sender is a Host forwarding an answer or candidate back to the Client
        else
        {
            var hostEntry = hosts.Values.FirstOrDefault(h => h.client == sender);
            if (hostEntry?.connectedClient != null)
            {
                _ = hostEntry.connectedClient.SendAsync(buffer.ToArray());
            }
        }
    }
}