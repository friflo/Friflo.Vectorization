using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using Friflo.TmGui;

namespace TmGui.SignalingServer;


public record SignalingMessage(string Type, string? SdpOrCandidate, string? Host);


public class GuiHost
{
    internal readonly string          name;
    internal readonly SignalingClient client;
    
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

        switch (message.Type)
        {
            case "register":
                if (!string.IsNullOrEmpty(message.Host)) {
                    // Safe upsert to prevent duplicate key crashes
                    hosts[message.Host] = new GuiHost(client, message.Host);
                }
                break;
                
            case "connect":
                if (!string.IsNullOrEmpty(message.Host)) {
                    if (hosts.TryGetValue(message.Host, out var host)) {
                        client.host = host;
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

    }
}