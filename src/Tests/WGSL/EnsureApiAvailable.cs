using Friflo.WGPU;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Tests.WGSL;


public static class EnsureApiAvailable
{
    public static void EnsureRenderPassApi(in RenderTarget target)
    {
        using var pass = target.BeginRenderPass(default);
        
        pass.SetScissorRect(1,2,3,4);
        pass.SetViewport(0,1,2,3,4,5);
        
        pass.SetBlendConstant([1,2,3,4]);
        pass.SetStencilReference(1);
        
        pass.PushDebugGroup("group");
        pass.InsertDebugMarker("marker");
        pass.PopDebugGroup();
        
        pass.BeginOcclusionQuery(1);
        pass.EndOcclusionQuery();
    }
    
    public static void EnsureIndirectStructs()
    {
        _ = new Indirect {
            vertexCount     = 0,
            instanceCount   = 0,
            firstVertex     = 0,
            firstInstance   = 0,
        };
        _ = new IndexedIndirect {
            indexCount      = 0,   
            instanceCount   = 0,
            firstIndex      = 0,
            baseVertex      = 0,
            firstInstance   = 0
        };
        
        _ = new DrawArgs {
            count           = 0,
            instanceCount   = 0,
            first           = 0,
            firstInstance   = 0,
        };
        
        _ = new DrawIndirectArgs {
            offset      = 0,
            drawCount   = 0,
        };
        _ = new DrawIndirectArgs(0, 0);
    }
}