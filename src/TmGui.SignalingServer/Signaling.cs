using System.Text;
using Friflo.TmGui;

namespace TmGui.SignalingServer;


/// <summary> Single <see cref="Signaling"/> instance is shared by all sessions. </summary>
public class Signaling
{
    public Signaling(TmGuiBackend backend)
    {
    }
    

    internal void ProcessMessage(SignalingClient client, ReadOnlySpan<byte> buffer)
    {
        string message = Encoding.UTF8.GetString(buffer);
    }
}