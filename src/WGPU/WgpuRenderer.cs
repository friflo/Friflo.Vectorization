using Friflo.SmGui;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

namespace WGPU;

/// <summary>
/// Default WGPU renderer for <see cref="SmGuiApp"/> instances.
/// You can copy and paste this class to customize the rendering pipeline or clear behavior.
/// </summary>
public sealed class WgpuRenderer : IWgpuRenderer
{
    public              SmGuiBackend?           GuiBackend { get; }
    private             SmGuiApp?               app;
    
    private readonly    WgpuBatch               batch;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    

    public void OnShutdown()
    {
        app?.Dispose();
        batch.Dispose();
    }
    
    public WgpuRenderer(WgpuHost wgpuHost)
    {
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets());
        GuiBackend      = wgpuBackend;
        batch           = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
    }
    
    public void CreateGuiApp(CreateGuiApp createGui) {
        app = createGui(GuiBackend!);
    }
    
    public void OnWindowChanged(int width, int height)
    {
        var c = app!.clearValue;
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new GpuColor(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f)
        };
    }
    
    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(10000);
        
        app?.RenderApp(batch, target.Width, target.Height);

        batch.DrawCommandList(target, renderPassDescriptor);
    }
}
