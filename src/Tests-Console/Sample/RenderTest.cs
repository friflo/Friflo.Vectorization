using System.Diagnostics;
using System.Numerics;
using Friflo.GPU;
using Friflo.WGPU;
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
    
    protected static Matrix4x4 GetTransformationMatrix(GpuExtent3D size, float time)
    {
        var proj = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, size.AspectRatio, 1f, 100f);
        var view = Matrix4x4.CreateRotationY(MathF.Sin(time))
                 * Matrix4x4.CreateTranslation(0, 0, -1.5f);
        return view * proj;
    }
    
    public virtual void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = target.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        myUniform.modelViewProjectionMatrix = GetTransformationMatrix(target.TargetSize, time);
        
        var timeUniform = new TimeUniform(time);
        var testUniform = new TestAddUniform(10);
        DeformVertices(target.ComputeContext, data.InOut(), testUniform, timeUniform);
        
        using var pass = target.BeginRenderPass(renderPassDescriptor);
        
        myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);
        var model_offset 		= new Vector2(0, 0.2f * MathF.Cos(time * 2));
        wormhood.iResolution    = new Vector3(target.TargetSize.width, target.TargetSize.height, 1.0f);
        wormhood.iTime          = time;
        
        Wormhood.RenderTunnel(pass, wgpu.Config, wormhood, new DrawArgs(3));
        DrawTriangles(pass, wgpu.Config, rectangle, myUniform, model_offset);
    }

	[Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<VertexData>    triangles,
        [Map(2, 0)] [uniform]           in MyUniforms           myUniform,
        [Map(2, 1)] [uniform]           Vector2                 model_offset);
    
    
    [Shader("~/shaders/renderTest/deform.wgsl", compute: "cs_main")]
    [WorkgroupSize(64)]
    private static partial void DeformVertices(PipelineContext computeContext,
        [Map(0, 0)] [storage] [Dispatch]    InOutBuffer<VertexData> vertices,
        [Map(0, 1)] [uniform]               TestAddUniform          testUniform,
        [Map(1, 0)] [uniform]               TimeUniform             uniform);
}



public static partial class Wormhood
{
    [Shader("~/shaders/renderTest/full_screen_triangle.wgsl",    vertex:   "vs_main")]
    [Shader("~/shaders/renderTest/raymarcher_no_texture.wgsl",   fragment: "fs_main")] // https://www.shadertoy.com/view/MdcSRj
    public static partial void RenderTunnel(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]   in ShadertoyUniforms    uniforms,
                                DrawArgs                args);
}