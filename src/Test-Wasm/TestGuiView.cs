using System.Numerics;
using Friflo.SmGui;



// ReSharper disable ConvertToPrimaryConstructor
namespace TuiTerminal;


/// <summary> Each session has its own <see cref="IGuiView"/> instance. </summary>
public class TestGuiView : IGuiView
{
    private readonly    ViewSession     session;

    
    
    private const int CanvasWidth   = 800;
    private const int CanvasHeight  = 500;
    
    public TestGuiView(ViewSession session)
    {
        this.session        = session;
        var uiType          = session.UiType;
        session.Timer.TickRate   = 60;
        session.Timer.Enabled    = uiType.IsGui();
        session.Action.SetWindowTitle(uiType == UiType.WebRtcGui ? $"{session.AppId} 🔀" : session.AppId ?? "App");
        session.Action.SetClearColor(0x999c9eff, 0x404142ff);
    }
    
    
    public void RenderView(SmBatch batch, int targetWidth, int targetHeight)
    {
        var gui = batch.BeginGui(targetWidth, targetHeight);
        using (gui.BeginWindow("Window 1", new Vector2(0, 0), new Vector2(1230, 1680), traits: 0)) { // (500, 450) (1000, 850)
            Window1(gui);
        }
    }
    
    private void Window1(Gui gui)
    {
        using (gui.BeginHorizontal()) {
            if (gui.Button("Web")) {
                gui.Action.OpenUrl($"https://localhost:8080", "web");
            }
            if (gui.Button("WebRTC")) {
                gui.Action.OpenUrl($"https://localhost:8081/?app={session.AppId}", "webrtc");
            }
            if (!gui.UiType.IsWeb()) {
                if (gui.Button("close session")) session.Action.CloseWindow();
            }
        }
        using (gui.BeginHorizontal()) {
            if (gui.Button("GC.Collect()"))  GC.Collect();
        }
    }
}