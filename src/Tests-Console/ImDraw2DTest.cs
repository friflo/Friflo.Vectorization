using System.Numerics;
using Friflo.Vectorization.WebGPU;
using Friflo.WGPU.ImDraw;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ImRenderer : IRenderer
{
    private readonly    Batcher2D               batcher;
    protected           GpuRenderPassDescriptor renderPassDescriptor= new () { colorAttachments = [ default ] };
    
    public void OnShutdown() {
        batcher.Dispose();
    }
    
    public ImRenderer(Wgpu wgpu)
    {
        batcher = new Batcher2D((WgpuDevice)wgpu.Device, wgpu.Config.Descriptor);
    }
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = [0.1, 0.1, 0.1, 1]
        };
    }
    
    public void OnFrame(in RenderFrame frame)
    {
        using var draw = batcher.BeginDraw2D(frame, renderPassDescriptor);
        
        draw.Rectangle(new Vector2(1, 1), new Vector2(40, 40), 0xFFFFFFFF);
        draw.Rectangle(new Vector2(frame.Width - 41, frame.Height - 41), new Vector2(40, 40), 0xFFFFFFFF);
        
        draw.Rectangle(new Vector2(100, 50), new Vector2(100, 100), Color32.Red);
        draw.Rectangle(new Vector2(300, 50), new Vector2(100, 100), Color32.Green);
        draw.Rectangle(new Vector2(500, 50), new Vector2(100, 100), Color32.Blue);
    }
}
