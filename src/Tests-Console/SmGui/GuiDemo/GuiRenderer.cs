using Friflo.SmGui;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

namespace GuiDemo;


public sealed class GuiRenderer : IRenderer
{
    public              SmGuiBackend?           GuiBackend { get; }
    private readonly    GuiApp                  app;
    
    private readonly    WgpuBatch               batch;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    

    public void OnShutdown()
    {
        app.Dispose();
        batch.Dispose();
    }
    
    public GuiRenderer(WgpuHost wgpuHost, CreateGuiApp createApp)
    {
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets());
        GuiBackend      = wgpuBackend;
        batch           = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
        
        app = createApp(GuiBackend);
    }
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new GpuColor(0.6f, 0.61f, 0.62f, 1f)
        };
    }
    
    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(10000);
        
        app.RenderApp(batch, target.Width, target.Height);

        batch.DrawCommandList(target, renderPassDescriptor);
    }
}
