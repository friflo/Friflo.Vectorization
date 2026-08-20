using System.Diagnostics;
using System.Numerics;
using Friflo.GPU;
using Friflo.WGPU;
using TestConsole;

namespace Shaders.Particles;

/* AI Prompt (Gemini Flash) 
Generate a GPU-driven particle system (Compute + Render) using WebGPU/WGSL and C# for the EGPS framework.

Requirements:
1. Provide the WGSL code for the compute shader (particle update) and render shader (instanced quads).
2. Do NOT generate any C# data structs (like Particle or FrameUniform), as they are handled automatically by the source generator.
3. Provide the C# Renderer class containing the partial methods for updating and rendering, strictly following this exact architectural template style and caching pattern:
[PASTE YOUR C# RENDERER CODE TEMPLATE HERE - e.g. from: RenderTest.cs]
*/
public partial class Renderer : IRenderer
{
    private readonly GpuBuffer<Particle>        particleBuffer;
    private readonly InView<Particle>           particleView;
    private readonly PerfLog                    perfLog      = new();
    private readonly Stopwatch                  stopwatch    = Stopwatch.StartNew();
    private readonly Wgpu                       wgpu;
    
    private readonly GpuRenderPassDescriptor    renderPassDescriptor = new() { colorAttachments = [default] };
    private          float                      lastTime;
    
    private const int ParticleCount = 100_000;

    public Renderer(Wgpu wgpu)
    {
        this.wgpu = wgpu;

        var initialParticles = GenerateInitialParticles(ParticleCount);

        // Buffer is used by Compute Read/Write and Rendering 
        particleBuffer = wgpu.Device.CreateBuffer(initialParticles, "particles", BufferProfile.InOut);
        particleView   = particleBuffer.In(0, ParticleCount);
    }

    private static Particle[] GenerateInitialParticles(int count)
    {
        var particles = new Particle[count];
        var rand = Random.Shared;

        for (int i = 0; i < count; i++)
        {
            particles[i] = new Particle
            {
                position = new Vector4(0f, -0.4f, 0f, (float)rand.NextDouble() * 2.5f),
                velocity = new Vector4(
                    ((float)rand.NextDouble() - 0.5f) * 1.5f,
                    (float)rand.NextDouble() * 0.8f + 0.4f,
                    ((float)rand.NextDouble() - 0.5f) * 0.5f,
                    0f
                )
            };
        }

        return particles;
    }

    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment
        {
            loadOp     = LoadOp.Clear,
            storeOp    = StoreOp.Store,
            clearValue = new GpuColor(0.05, 0.05, 0.08, 1.0)
        };
    }

    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = target.View;

        var currentTime = (float)stopwatch.Elapsed.TotalSeconds;
        var deltaTime   = currentTime - lastTime;
        lastTime        = currentTime;

        if (deltaTime > 0.1f) deltaTime = 0.016f;

        var frameData = new FrameUniform {
            time      = currentTime,
            deltaTime = deltaTime
        };

        UpdateParticles(target.ComputeContext, particleBuffer.InOut(), frameData);

        using var pass = target.BeginRenderPass(renderPassDescriptor);
        DrawParticles(pass, wgpu.Config, particleView, frameData, new DrawArgs(6, ParticleCount));
    }

    public void OnShutdown()
    {
        particleBuffer.Dispose();
    }

    [Shader("~/shaders/particles/update.wgsl", compute: "cs_main")]
    [WorkgroupSize(256)]
    private static partial void UpdateParticles(PipelineContext computeContext,
        [Map(0, 0)] [storage] [Dispatch] InOutBuffer<Particle> particles,
        [Map(0, 1)] [uniform]            FrameUniform          frameData);

    [Shader("~/shaders/particles/render.wgsl", vertex: "vs_main", fragment: "fs_main")]
    private static partial void DrawParticles(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<Particle>  particles,
        [Map(1, 0)] [uniform]           FrameUniform        frameData,
                                        DrawArgs            args);
}
