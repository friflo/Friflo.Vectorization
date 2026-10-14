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


internal record struct SignalingMessage(string type, string? app, string? connection, string? sdpOrCandidate)
{
    public override string ToString() => $"[{type}]  app: {app}  conn: {connection}";
}

public class TmGuiApp
{
    public   readonly   string              name;
    internal readonly   SignalingClient     client;
    internal            SignalingClient?    connectedClient; // The client currently connected to this app

    public   override   string              ToString() => name;

    internal TmGuiApp(SignalingClient client, string name) {
        this.name   = name;
        this.client = client;
    }
}

internal partial class SignalingClient
{
    private  readonly   WebSocket   webSocket;
    internal            TmGuiApp?   guiApp;       // the GUI this client wants to connect to
    internal readonly   string      connection;
    
    public   override   string      ToString() => $"{(guiApp == null ? "App" : "Client")}  conn: {connection}";
    

    internal SignalingClient(WebSocket webSocket, string connection)
    {
        this.webSocket  = webSocket;
        this.connection = connection;
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
    public readonly ConcurrentDictionary<string, TmGuiApp> guiApps = [];
    
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
                if (!string.IsNullOrEmpty(message.app)) {
                    Console.WriteLine($"Register app: '{message.app}'");
                    guiApps[message.app] = new TmGuiApp(client, message.app);
                }
                break;
                
            case "connect":
                if (!string.IsNullOrEmpty(message.app)) {
                    var foundApp = guiApps.TryGetValue(message.app, out var guiApp);
                    if (foundApp) {
                        client.guiApp = guiApp;
                        guiApp!.connectedClient = client; // Link the client to this app session
                    }
                    var result = foundApp ? "Found" : "NotFound";
                    var msg = new SignalingMessage("ConnectResponse", message.app, client.connection, result);
                    var errorResponse = JsonSerializer.SerializeToUtf8Bytes(msg);
                    _ = client.SendAsync(errorResponse);
                    Console.WriteLine($"Connect app: '{message.app}'  {result}");
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
        message             = message with { connection =  sender.connection };
        var routeMessage    = JsonSerializer.SerializeToUtf8Bytes(message);
        
        // Case 1: Sender is a Client forwarding an offer or candidate to the Host
        if (sender.guiApp != null && sender != sender.guiApp.client)
        {
            _ = sender.guiApp.client.SendAsync(routeMessage);
        }
        // Case 2: Sender is a Host forwarding an answer or candidate back to the Client
        else
        {
            var guiApp = guiApps.Values.FirstOrDefault(h => h.client == sender);
            if (guiApp?.connectedClient != null)
            {
                _ = guiApp.connectedClient.SendAsync(routeMessage);
            }
        }
    }
}