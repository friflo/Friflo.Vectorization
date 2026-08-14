using System.Numerics;
using Friflo.Vectorization.WebGPU;
using Friflo.WGPU.ImDraw;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ImGuiRenderer : IRenderer
{
    private readonly    Batch2D                 batch;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    
    public void OnShutdown() {
        batch.Dispose();
    }
    
    public ImGuiRenderer(Wgpu wgpu)
    {
        var device = wgpu.Device;
        batch = new Batch2D(device, wgpu.SwapChainFormat);
    }
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = [0.1, 0.1, 0.1, 1]
        };
    }

    public void OnEvent(in ImEvent ev)
    {
        batch.AddEvent(ev);
    }

    public void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(10000);
        
        using var gui = batch.BeginGui(frame, renderPassDescriptor);

        gui.BeginWindow("Test Window", new Vector2(100, 20), new Vector2(1000, 600), 0xaaaaaaff);
        
        gui.Label("hello GUI");
        if (gui.Button("hello")) {
            Console.WriteLine("Clicked: hello");
        }
        if (gui.Button("world", 0x7777ffff)) {
            Console.WriteLine("Clicked: world");
        }
        gui.EndWindow();
    }
}
