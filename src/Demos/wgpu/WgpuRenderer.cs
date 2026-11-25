using Friflo.SmGui;
using Friflo.SmGui.WGPU;
using Friflo.WGPU.SDL3;

namespace Friflo.WGPU;

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
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets()) {
            RuntimeEventHandler = Sdl3EventHandler.Instance
        };
        GuiBackend  = wgpuBackend;
        batch       = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
    }
    
    public SmGuiApp CreateGuiApp(CreateGuiApp createGui) {
        return app = createGui(GuiBackend!);
    }
    
    public void OnWindowChanged(int width, int height)
    {
        var color = app!.ClearValue ?? 0x999C9EFF;
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = color.AsGpuColor()
        };
    }
    
    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(10000);
        
        app?.RenderApp(batch, target.Width, target.Height);

        batch.DrawCommandList(target, renderPassDescriptor);
    }
}
