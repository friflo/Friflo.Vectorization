using System;
using System.Diagnostics;
using System.Numerics;
using Friflo.SmGui;



// ReSharper disable ConvertToPrimaryConstructor
namespace Demos.Wasm;


/// <summary> Each session has its own <see cref="GuiView"/> instance. </summary>
public class TestGuiView : GuiView
{
    private long    frameStart = Stopwatch.GetTimestamp();
    private double  frameTime;
    private long    memStart;
    private long    memDiff;
    private long    renderMem;
    
    public TestGuiView(ViewSession session) : base(session)
    {
        var uiType  = session.UiType;
        var appId   = session.Config.AppId;
        session.Timer.Enabled    = uiType.IsGui();
        session.Action.SetWindowTitle(uiType == UiType.WebRtcGui ? $"{appId} 🔀" : appId ?? "App");
        session.Action.SetClearColor(0x999c9eff, 0x404142ff);
    }
    
    
    public override void RenderView(SmBatch batch, int targetWidth, int targetHeight)
    {
        var timestamp   = Stopwatch.GetTimestamp();
        frameTime       = Stopwatch.GetElapsedTime(frameStart, timestamp).TotalMilliseconds;
        frameStart      = timestamp;
        
        var mem     = GC.GetAllocatedBytesForCurrentThread();
        memDiff     = mem - memStart;
        memStart    = mem;
        
        var gui = batch.BeginGui(targetWidth, targetHeight);
        using (gui.BeginWindow("Window 1", new Vector2(0, 0), new Vector2(1230, 1680), traits: 0)) { // (500, 450) (1000, 850)
            Window1(gui);
        }
        renderMem   = gui.widget.DebugMem - mem;
    }
    
    private void Window1(Gui gui)
    {
        using (gui.BeginHorizontal()) {
            if (gui.Button("Web")) {
                gui.Action.OpenUrl($"https://localhost:{DemoConfig.HttpsPort}", "web");
            }
            if (gui.Button("WebRTC")) {
                gui.Action.OpenUrl(session.Config.WebRtcAppUrl, "webrtc");
            }
            if (!gui.UiType.IsWeb()) {
                if (gui.Button("close session")) session.Action.CloseWindow();
            }
        }
        using (gui.BeginHorizontal()) {
            if (gui.Button("GC.Collect()"))  GC.Collect();
        }
        var animate = gui.Timer.Enabled;
        if (gui.Checkbox("animate", ref animate)) {
            gui.Timer.Enabled = animate;
            session.SetAppDirty();
        }
        
        var sb = gui.widget.StringBuilder();
        sb.Append("frame time: ");
        var provider = gui.widget.FormatProvider;
        if (gui.Batch.Timer.Enabled) {
            sb.AppendDouble(frameTime, "F1", provider);
            sb.Append(" ms");
        } else {
            sb.Append(" ---");
        }
        sb.Append(" alloc: ");
        sb.AppendDouble(memDiff, "F0", provider);
        sb.Append(' ');
        sb.AppendDouble(renderMem, "F0", provider);
        gui.Label(sb.Span(), Color32.Teal);
    }
}