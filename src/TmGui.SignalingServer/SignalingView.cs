using System.Numerics;
using Friflo.TmGui;
using Friflo.TmGui.TUI;


// ReSharper disable ConvertToPrimaryConstructor
namespace TmGui.SignalingServer;


/// <summary> Each session has its own <see cref="IGuiView"/> instance. </summary>
public class SignalingView : IGuiView
{
    private readonly Signaling signaling;
    
    public SignalingView(Signaling signaling, SessionInfo info)
    {
        this.signaling = signaling;
    }
    
    public void RenderGui(TmBatch batch, int targetWidth, int targetHeight)
    {
        var gui = batch.BeginGui(targetWidth, targetHeight);
        using (gui.BeginWindow("Signaling", new Vector2(0, 0), new Vector2(1230, 1680), traits: 0, TuiBorder.Rounded)) { // (500, 450) (1000, 850)
            Window1(gui);
        }
    }
    
    private void Window1(Gui gui)
    {
        gui.Button("hello Signaling", Dim.Fill_X(0, Fit.Content));

        foreach (var host in signaling.hosts)
        {
            gui.Button(host.name);
        }
    }
}