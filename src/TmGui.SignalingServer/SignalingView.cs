using System.Numerics;
using Friflo.SmGui;
using Friflo.SmGui.Http;
using Friflo.SmGui.TUI;

// ReSharper disable ConvertToPrimaryConstructor
namespace SmGui.SignalingServer;


/// <summary> Each session has its own <see cref="IGuiView"/> instance. </summary>
public class SignalingView : IGuiView
{
    private readonly    SmSignaling         signaling;
    private readonly    List<SmGuiApp>      apps    = [];
    private readonly    List<SmSignalPeer>  clients = [];
    
    public SignalingView(SmSignaling signaling, ViewSession session)
    {
        this.signaling = signaling;
        session.Action.SetWindowTitle("Signaling Server");
    }
    
    public void RenderView(SmBatch batch, int targetWidth, int targetHeight)
    {
        var gui = batch.BeginGui(targetWidth, targetHeight);
        using (gui.BeginWindow("WebRTC apps", new Vector2(50, 50), new Vector2(600, 700), traits: 0, TuiBorder.Rounded)) { // (500, 450) (1000, 850)
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
        gui.Label("registered apps");
        gui.Spacer();
        
        foreach (var guiApp in apps)
        {
            using (gui.BeginHorizontal()) {
                if (gui.Button(guiApp.name)) {
                    gui.Action.OpenUrl(guiApp.url, guiApp.name);
                }
                if (guiApp.Online) {
                    gui.Label("sessions: ");
                    var sb = gui.widget.StringBuilder().Append(guiApp.SessionCount);
                    gui.Label(sb.Span());
                } else {
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
        gui.Label("connecting clients"); // WebRTC uses term: "negotiation"
        gui.Spacer();
        
        foreach (var client in clients)
        {
            gui.Label(client.connection);
        }
    }
}