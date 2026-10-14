using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


internal record struct SignalingMessage(string type, string? sdpOrCandidate, string? host, string? client)
{
    public override string ToString() => $"{type} > host: {host}  client: {client}";
}

public class GuiHost
{
    public   readonly   string              name;
    internal readonly   SignalingClient     client;
    internal            SignalingClient?    connectedClient; // The client currently connected to this host

    public   override   string              ToString() => name;

    internal GuiHost(SignalingClient client, string name) {
        this.name   = name;
        this.client = client;
    }
}

internal partial class SignalingClient
{
    private  readonly   WebSocket   webSocket;
    internal            GuiHost?    host;       // the GUI this client wants to connect to
    internal readonly   string      client;
    
    public   override   string      ToString() => client;
    

    internal SignalingClient(WebSocket webSocket, string clientId)
    {
        this.webSocket  = webSocket;
        this.client     = clientId;
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
    public readonly ConcurrentDictionary<string, GuiHost> hosts = [];
    
    public Signaling(TmGuiBackend backend)
    {
    }
    
    private static bool TryParse(ReadOnlySpan<byte> buffer, out SignalingMessage message)
    {
        try {
            message = JsonSerializer.Deserialize<SignalingMessage>(buffer);
            return true;
        }
        catch (JsonException) {
            message = default;
            return false;
        }
    }
    
    internal void ProcessMessage(SignalingClient client, ReadOnlySpan<byte> buffer)
    {
        if (!TryParse(buffer, out var message)) {
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
                        var msg = new SignalingMessage("ConnectResponse", "Found", message.host, client.client);
                        var successResponse = JsonSerializer.SerializeToUtf8Bytes(msg);
                        _ = client.SendAsync(successResponse);
                    }
                    else {
                        // Report that the host was not found
                        var msg = new SignalingMessage("ConnectResponse", "NotFound", message.host, client.client);
                        var errorResponse = JsonSerializer.SerializeToUtf8Bytes(msg);
                        _ = client.SendAsync(errorResponse);
                    }
                }
                break;

            case "offer":
            case "answer":
            case "candidate":
                RouteWebRtcMessage(client, message);
                break;
        }
    }

    private void RouteWebRtcMessage(SignalingClient sender, SignalingMessage message)
    {
        message             = message with { client =  sender.client };
        var routeMessage    = JsonSerializer.SerializeToUtf8Bytes(message);
        
        // Case 1: Sender is a Client forwarding an offer or candidate to the Host
        if (sender.host != null && sender != sender.host.client)
        {
            _ = sender.host.client.SendAsync(routeMessage);
        }
        // Case 2: Sender is a Host forwarding an answer or candidate back to the Client
        else
        {
            var hostEntry = hosts.Values.FirstOrDefault(h => h.client == sender);
            if (hostEntry?.connectedClient != null)
            {
                _ = hostEntry.connectedClient.SendAsync(routeMessage);
            }
        }
    }
}