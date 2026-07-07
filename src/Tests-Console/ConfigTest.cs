using Friflo.Vectorization.WebGPU;

// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class ConfigTest : RenderTest
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
        
        using var pass = frame.BeginRenderPass(renderPassDescriptor);
        
        myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);

        DrawTriangles(pass, config, rectangle, myUniform);
    }
}
