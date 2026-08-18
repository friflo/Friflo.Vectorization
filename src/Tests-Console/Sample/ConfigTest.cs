using Friflo.WGPU;
using TestConsole;

// ReSharper disable ConvertToPrimaryConstructor
namespace Shaders.RenderTest;

public class ConfigTest : Renderer
{
    public ConfigTest(Wgpu wgpu) : base(wgpu)
    {
        var desc = wgpu.Config.Descriptor;
        desc.PrimitiveState.cullMode = CullMode.Front;
        testConfig = desc.CreateConfig("testConfig");
    }

    private readonly RenderConfig testConfig;
    
    
    public override void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time    = (float)stopwatch.Elapsed.TotalSeconds;
        var config  = perfLog.FrameCount % 2 == 0 ? testConfig : wgpu.Config;
        myUniform.modelViewProjectionMatrix = GetTransformationMatrix(frame.WindowSize, time);
        
        using var pass = frame.BeginRenderPass(renderPassDescriptor);
        
        myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);

        DrawTriangles(pass, config, rectangle, myUniform, default);
    }
}
