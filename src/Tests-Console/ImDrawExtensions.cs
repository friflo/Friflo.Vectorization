
using Friflo.WGPU;
using Friflo.WGPU.ImDraw;

namespace TestConsole;

public static class ImDrawExtensions
{
    
    public static ImTexture ToImTexture(this GpuTextureView view)
    {
        return new ImTexture(view.texture, view.Handle);
    }
}