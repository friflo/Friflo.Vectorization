using System.Diagnostics;
using System.Numerics;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using TestConsole;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Shaders.RenderTest;

/// <summary>
/// Uses an event driven approach - DrawFrame() + Shutdown() - instead of running an event loop.<br/>
/// This approach ensures the same renderer can be used on mobile devices or browsers without code changes.<br/>
/// Those platforms only support event driven applications. An event loop would lead to application freeze.
/// </summary>
public partial class Renderer : IRenderer
{
    // --- IDisposable fields
    protected readonly  GpuBuffer<VertexData>   data;
    
    public void OnShutdown()
    {
        data.Dispose();
    }
    
    public Renderer(Wgpu wgpu)
    {
        this.wgpu = wgpu;
        data        = wgpu.Device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        rectangle   = data.In(0, 6); // two triangles
    }

    private static readonly VertexData[] Vertices =
    [
        new(new Vector4(-0.5f,  0.5f, 0, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),  // Top-Left
        new(new Vector4(-0.5f, -0.5f, 0, 1), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),  // Bottom-Left
        new(new Vector4( 0.5f, -0.5f, 0, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f)),  // Bottom-Right
        
        new(new Vector4(-0.5f,  0.5f, 0, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),  // Top-Left
        new(new Vector4( 0.5f, -0.5f, 0, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f)),  // Bottom-Right
        new(new Vector4( 0.5f,  0.5f, 0, 1), new Vector4(1.0f, 1.0f, 1.0f, 1.0f))   // Top-Right
    ];

    // --- non-disposable fields
    protected readonly  Wgpu                    wgpu;
    protected readonly  PerfLog                 perfLog             = new();
    protected readonly  InView<VertexData>      rectangle;
    protected           MyUniforms              myUniform           = new() { tint_color = new Vector4(1, 1, 0, 1) };
    protected           ShadertoyUniforms       wormhood;
    protected readonly  Stopwatch               stopwatch           = Stopwatch.StartNew();
    protected           GpuRenderPassDescriptor renderPassDescriptor= new () { colorAttachments = [ default ] };
    
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = [0.1, 0.1, 0.1, 1]
        };
    }
    
    protected static Matrix4x4 GetTransformationMatrix(float width, float height, float time)
    {
        var proj = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, width / height, 1f, 100f);
        var view = Matrix4x4.CreateRotationY(MathF.Sin(time))
                 * Matrix4x4.CreateTranslation(0, 0, -1.5f);
        return view * proj;
    }
    
    public virtual void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        var timeUniform = new TimeUniform(time);
        myUniform.modelViewProjectionMatrix = GetTransformationMatrix(frame.Width, frame.Height, time);
        
        DeformVertices(frame.Context, data.InOut(), timeUniform);
        
        using var pass = frame.BeginRenderPass(renderPassDescriptor);
        
        myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);
        var model_offset 		= new Vector2(0, 0.2f * MathF.Cos(time * 2));
        wormhood.iResolution    = new Vector3(frame.Width, frame.Height, 1.0f);
        wormhood.iTime          = time;
        
        Wormhood.RenderTunnel(pass, wgpu.Config, wormhood);
        DrawTriangles(pass, wgpu.Config, rectangle, myUniform, model_offset);
    }

	[Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<VertexData>    triangles,
        [Map(2, 0)] [uniform]           in MyUniforms           myUniform,
        [Map(2, 1)] [uniform]           Vector2                 model_offset);
    
    
    [NoEmit]
    [Shader("~/shaders/renderTest/deform.wgsl", compute: "cs_main")]
    private static partial void DeformVertices(PipelineContext context,
        [Map(0, 0)] [storage] [Compute] InOutBuffer<VertexData> vertices,
        [Map(1, 0)] [uniform]           TimeUniform             uniform);
}



public static partial class Wormhood
{
    [Shader("~/shaders/renderTest/full_screen_triangle.wgsl",    vertex:   "vs_main")]
    [Shader("~/shaders/renderTest/raymarcher_no_texture.wgsl",   fragment: "fs_main")] // https://www.shadertoy.com/view/MdcSRj
    [DrawVertexIndex(3, 1)]
    public static partial void RenderTunnel(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform] in ShadertoyUniforms uniforms);
}