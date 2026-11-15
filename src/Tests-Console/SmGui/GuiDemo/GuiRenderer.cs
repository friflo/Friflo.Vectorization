using Friflo.SmGui;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

namespace GuiDemo;


public class GuiRenderer : IRenderer
{
    public              SmGuiBackend?           GuiBackend { get; }
    
    private readonly    WgpuBatch               batch;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    
    private readonly    IGuiApp                 app;

    public void OnShutdown()
    {
        app.Dispose();
        batch.Dispose();
    }
    
    public GuiRenderer(WgpuHost wgpuHost, Func<SmGuiBackend, IGuiApp> createApp)
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
        
        app.OnFrame(batch, target.Width, target.Height);

        batch.DrawCommandList(target, renderPassDescriptor);
    }
}
