using System.Numerics;
using Friflo.TmGui;
using Friflo.TmGui.Http;
using Friflo.TmGui.TUI;

// ReSharper disable ConvertToPrimaryConstructor
namespace TmGui.SignalingServer;


/// <summary> Each session has its own <see cref="IGuiView"/> instance. </summary>
public class SignalingView : IGuiView
{
    private readonly    TmSignaling         signaling;
    private readonly    List<TmGuiApp>      apps    = [];
    private readonly    List<TmSignalPeer>  clients = [];
    
    public SignalingView(TmSignaling signaling, SessionInfo info)
    {
        this.signaling = signaling;
    }
    
    public void RenderGui(TmBatch batch, int targetWidth, int targetHeight)
    {
        var gui = batch.BeginGui(targetWidth, targetHeight);
        using (gui.BeginWindow("Signaling Server for WebRTC", new Vector2(50, 50), new Vector2(600, 700), traits: 0, TuiBorder.Rounded)) { // (500, 450) (1000, 850)
            Apps(gui);
        }
        using (gui.BeginWindow("Web clients", new Vector2(700, 50), new Vector2(600, 700), traits: 0, TuiBorder.Rounded)) { // (500, 450) (1000, 850)
            Clients(gui);
        }
    }
    
    private void Apps(Gui gui)
    {
        apps.Clear();
        apps.AddRange(signaling.guiApps.Values);
        apps.Sort((app1, app2) => app1.registered.CompareTo(app2.registered));
        gui.Label("apps");
        gui.Spacer();
        
        foreach (var guiApp in apps)
        {
            using (gui.BeginHorizontal()) {
                if (gui.Button(guiApp.name)) {
                    gui.Action.OpenUrl(guiApp.url, guiApp.name);
                }
                if (!guiApp.online) {
                    gui.Label("offline");
                }
            }
        }
    }
    
    private void Clients(Gui gui)
    {
        clients.Clear();
        clients.AddRange(signaling.clients.Values);
        clients.Sort((c1, c2) => c1.registered.CompareTo(c2.registered));
        gui.Label("clients");
        gui.Spacer();
        
        foreach (var client in clients)
        {
            gui.Label(client.connection);
        }
    }
}