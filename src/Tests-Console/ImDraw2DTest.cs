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
    private readonly    RenderConfig            config;
    protected           GpuRenderPassDescriptor renderPassDescriptor= new () { colorAttachments = [ default ] };
    
    public void OnShutdown() { }
    
    public ImRenderer(Wgpu wgpu)
    {
        batcher = new Batcher2D((WgpuDevice)wgpu.Device);
        config  = wgpu.Config;
    }
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = [0.1, 0.1, 0.1, 1]
        };
    }
    
    public virtual void OnFrame(in RenderFrame frame)
    {
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        
        using var pass      = frame.BeginRenderPass(renderPassDescriptor);
        using var draw2D    = new Draw2D(batcher, pass, config);
        
        draw2D.Rectangle(new Vector2(0, 0),     new Vector2(0.5f, 0.5f), 0xFF0000FF);
        draw2D.Rectangle(new Vector2(10, 10),   new Vector2(  10,   10), 0xFF0000FF);
        draw2D.Rectangle(new Vector2(100, 100), new Vector2( 100,  100), 0xFF0000FF);
        
        draw2D.Flush();
    }
}
