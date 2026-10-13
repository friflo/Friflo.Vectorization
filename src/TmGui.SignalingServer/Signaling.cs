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


/// <summary> Single <see cref="Signaling"/> instance is shared by all sessions. </summary>
public class Signaling
{
    internal readonly Dictionary<string, GuiHost> hosts = [];
    
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
                if (message.Host != null) {
                    hosts.Add(message.Host, new GuiHost(client, message.Host));
                }
                break;
        }
    }
}