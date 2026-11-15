using Friflo.SmGui;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

namespace DrawDemo;


public class DrawRenderer : IRenderer
{
    public              SmGuiBackend?           GuiBackend { get; }
    
    private readonly    WgpuBatch               batch;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    
    private readonly    GuiApp                  app;

    public void OnShutdown()
    {
        app.Dispose();
        batch.Dispose();
    }
    
    public DrawRenderer(WgpuHost wgpuHost, Func<SmGuiBackend, GuiApp> createApp)
    {
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets());
        GuiBackend  = wgpuBackend;
        batch       = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
        
        app = createApp(GuiBackend);
    }
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new GpuColor(0.1, 0.1, 0.1, 1)
        };
    }
    
    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(5000);
        
        app.RenderApp(batch, target.Width, target.Height);
        
        batch.DrawCommandList(target, renderPassDescriptor);
    }
}
