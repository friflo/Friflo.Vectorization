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
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        
        using var pass  = frame.BeginRenderPass(renderPassDescriptor);
        using var draw  = new Draw2D(batcher, pass);
        
        draw.SetViewport(frame.Width, frame.Height);
        
        draw.Rectangle(new Vector2(  0,   0), new Vector2(0.5f, 0.5f), 0xFF0000FF);
        draw.Rectangle(new Vector2( 10,  10), new Vector2(  10,   10), 0xFF0000FF);
        draw.Rectangle(new Vector2(100, 100), new Vector2( 100,  100), 0xFF0000FF);
        draw.Rectangle(new Vector2(300, 100), new Vector2( 100,  100), 0xFF0000FF);
        
        draw.Flush(); // redundant call
    }
}
