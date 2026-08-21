using Friflo.WGPU;
using TestConsole;

// ReSharper disable ConvertToPrimaryConstructor
namespace Shaders.RenderTest;

public class ConfigTest : Renderer
{
    public ConfigTest(WgpuHost wgpuHost) : base(wgpuHost)
    {
        var desc = wgpuHost.Config.Descriptor;
        desc.PrimitiveState.cullMode = CullMode.Front;
        testConfig = desc.CreateConfig("testConfig");
    }

    private readonly RenderConfig testConfig;
    
    
    public override void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = target.View;
        var time    = (float)stopwatch.Elapsed.TotalSeconds;
        var config  = perfLog.FrameCount % 2 == 0 ? testConfig : wgpuHost.Config;
        myUniform.modelViewProjectionMatrix = GetTransformationMatrix(target.TargetSize, time);
        
        using var pass = target.BeginRenderPass(renderPassDescriptor);
        
        myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);

        DrawTriangles(pass, config, rectangle, myUniform, default);
    }
}
