using System.Text.Json;
using Friflo.TmGui;

namespace TmGui.SignalingServer;


// 1. Define your data model matching the JSON structure
public record SignalingMessage(string Type, string SdpOrCandidate, string? Host);


public class GuiHost
{
    public readonly string name;
    
    public GuiHost(string name) {
        this.name = name;
    }
}


/// <summary> Single <see cref="Signaling"/> instance is shared by all sessions. </summary>
public class Signaling
{
    internal readonly List<GuiHost> hosts = [];
    
    public Signaling(TmGuiBackend backend)
    {
    }
    

    internal void ProcessMessage(SignalingClient client, ReadOnlySpan<byte> buffer)
    {
        try
        {
            var message = JsonSerializer.Deserialize<SignalingMessage>(buffer);
            if (message != null)
            {
                if (message.Type == "register") {
                    if (message.Host != null) {
                        hosts.Add(new GuiHost(message.Host));
                    }
                }
                // Handle valid signaling message
            }
        }
        catch (JsonException)
        {

        }
    }
}