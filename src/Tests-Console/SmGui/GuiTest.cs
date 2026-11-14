// --- GuiTest.cs

using Friflo.SmGui;
using Friflo.SmGui.Session;
using Friflo.SmGui.WGPU;
using Friflo.WGPU;

namespace TestConsole;

public class SmGuiRenderer : IRenderer
{
    private readonly    WgpuBatch               gpuBatch;
    private readonly    GpuRenderPassDescriptor renderPassDescriptor    = new () { colorAttachments = [ default ] };
    private readonly    PerfLog                 perfLog                 = new();
    private readonly    AppResources            resources;
    private readonly    GuiView                 nativeView;
    private readonly    SmSessionLoop           loop;
    
    public SmGuiBackend? GuiBackend { get; }

    public void OnShutdown()
    {
        loop.Dispose();
        resources.Dispose();
        gpuBatch.Dispose();
    }
    
    public SmGuiRenderer(WgpuHost wgpuHost)
    {
        var wgpuBackend = new WgpuGuiBackend(wgpuHost.Device, new DefaultGuiAssets());
        GuiBackend      = wgpuBackend;
        gpuBatch        = wgpuBackend.CreateBatch(wgpuHost.SwapChainFormat);
        resources       = new AppResources(GuiBackend);
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
        _ = gpuBatch.Timer.Enabled;    // not relevant - only smoke test to ensure Timer instance != null
        
        perfLog.Trace(10000);
        
        loop.IterateSessions();
        
        nativeView.RenderView(gpuBatch, target.Width, target.Height);

        gpuBatch.DrawCommandList(target, renderPassDescriptor);
    }
}
