using Friflo.Vectorization.WebGPU;


// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ImRenderer : IRenderer
{
    
    public void OnShutdown() { }
    
    public ImRenderer(Wgpu wgpu) { }
    
    
    public void OnWindowChanged(int width, int height)
    {
        /* renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = [0.1, 0.1, 0.1, 1]
        }; */
    }
    
    public virtual void OnFrame(in RenderFrame frame)
    {
    }
}
