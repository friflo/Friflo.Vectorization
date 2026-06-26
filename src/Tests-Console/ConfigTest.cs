using System.Numerics;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

/// <summary>
/// Uses an event driven approach - DrawFrame() + Shutdown() - instead of running an event loop by yourself.<br/>
/// This approach ensures the same renderer can be used on mobile devices or browsers without code changes.<br/>
/// Those platforms only support event driven applications. An event loop would lead to application freeze.
/// </summary>
public class ConfigTest : RenderTest
{
    public ConfigTest(Wgpu wgpu) : base(wgpu)
    {
        var desc = wgpu.Config.Descriptor;
        desc.PrimitiveState.cullMode = CullMode.Front;
        testConfig = desc.CreateConfig("testConfig");
    }

    private readonly RenderConfig testConfig;
    
    
    public override void OnFrame(int width, int height)
    {
        using var frame = context.BeginFrame(wgpu.Surface);
        if (frame.IsNull) {     // window minimized?
            return;
        }
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time    = (float)stopwatch.Elapsed.TotalSeconds;
        var config  = perfLog.FrameCount % 2 == 0 ? testConfig : wgpu.Config;
        
        using (var pass = frame.BeginRenderPass<MainWorld>(renderPassDescriptor, config))
        {
            myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);
            wormhood.IResolution    = new Vector3(width, height, 1.0f);
            wormhood.ITime          = time;
            
            // Wormhood.RenderTunnel(pass, wormhood);
            DrawTriangles(pass, rectangle, myUniform);
        }
        context.Queue.Submit();
        wgpu.Surface.Present();
    }
}
