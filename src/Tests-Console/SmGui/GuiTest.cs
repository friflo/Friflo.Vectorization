// --- GuiTest.cs

using Friflo.SmGui;
using Friflo.SmGui.Session;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

namespace TestConsole;


public class SmGuiRenderer : IRenderer
{
    public              SmGuiBackend?           GuiBackend { get; }
    
    private readonly    WgpuBatch               batch;
    private readonly    GuiAppResources         resources;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    private readonly    GuiView                 nativeView;
    private readonly    SmSessionLoop           loop;

    public void OnShutdown()
    {
        loop.Dispose();
        resources.Dispose();
        batch.Dispose();
    }
    
    public SmGuiRenderer(WgpuHost wgpuHost)
    {
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets());
        GuiBackend      = wgpuBackend;
        batch           = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
        resources       = new GuiAppResources(GuiBackend);
        nativeView      = new GuiView(resources, null);
        loop            = GuiView.CreateSessionLoop(GuiBackend, resources);
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
        
        loop.IterateSessions();
        
        nativeView.RenderView(batch, target.Width, target.Height);

        batch.DrawCommandList(target, renderPassDescriptor);
    }
}
