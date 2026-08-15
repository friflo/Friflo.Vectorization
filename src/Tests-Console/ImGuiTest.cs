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
    private             bool                    mouseCircle;
    private             bool                    enabled2;
    private             float                   volume;
    
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
        
        batch.input.NewFrame();
        using var gui = batch.BeginGui(frame, renderPassDescriptor);
        
        gui.SetNextWindowPos(new Vector2(100, 20));
        gui.SetNextWindowSize(new Vector2(500, 600));
        gui.BeginWindow("Window 1", 0xaaaaaaff);
        
        gui.Label("hello GUI");
        gui.Label("");
        if (gui.Button("hello"))                            Console.WriteLine("Clicked: hello");
        if (gui.Button("world", 0x7777ffff))                Console.WriteLine("Clicked: world");
        
        gui.Label("");
        if(gui.Checkbox("mouse circle", ref mouseCircle))   Console.WriteLine("Clicked: checkbox");
        gui.Label("");
        if (gui.Slider(200, "Volume", ref volume, "F1", 0f, 1f)) Console.WriteLine($"Volume: changed");
        gui.Label("");
        
        gui.BeginHorizontal();
        if (gui.Button("Left"))                             Console.WriteLine("Clicked: Left");
        if (gui.Button("Right"))                            Console.WriteLine("Clicked: Right");
        gui.EndHorizontal();
        
        gui.EndWindow();
        
        gui.SetNextWindowPos(new Vector2(650, 20));
        gui.SetNextWindowSize(new Vector2(500, 600));
        gui.BeginWindow("Window 2", 0xaaaaaaff);
        gui.Checkbox("checkbox", ref enabled2);
        gui.EndWindow();
        
        if (mouseCircle) {
            gui.draw.PushZIndex(10);
            gui.draw.CircleLines(batch.input.Mouse, radius: 40f, 4, color: 0xFF0000FF, segments: 32);
        }
    }
}
