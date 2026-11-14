using Friflo.SmGui;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;


public class SmDrawRenderer : IRenderer
{
    private readonly    WgpuBatch               batch;
    private readonly    DrawAppResources        resources;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    private readonly    DrawView                nativeView;
    
    public void OnShutdown() {
        resources.Dispose();
        batch.Dispose();
    }
    
    public SmGuiBackend? GuiBackend { get; }

    public SmDrawRenderer(WgpuHost wgpuHost)
    {
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets());
        GuiBackend  = wgpuBackend;
        batch       = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
        resources   = new DrawAppResources(GuiBackend);
        nativeView  = new DrawView(resources);
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
        
        nativeView.RenderView(batch, target.Width, target.Height);
        
        batch.DrawCommandList(target, renderPassDescriptor);
    }
}
