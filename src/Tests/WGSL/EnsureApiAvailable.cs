using Friflo.Vectorization.WebGPU;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Tests.WGSL;


public static class EnsureApiAvailable
{
    public static void EnsureRenderPassApi(in RenderFrame frame)
    {
        using var pass = frame.BeginRenderPass(default);
        pass.SetBlendConstant([1,2,3,4]);
        pass.SetScissorRect(1,2,3,4);
        pass.SetViewport(0,1,2,3,4,5);
        pass.SetStencilReference(1);
        
        pass.PushDebugGroup("group");
        pass.InsertDebugMarker("marker");
        pass.PopDebugGroup();
    }
}