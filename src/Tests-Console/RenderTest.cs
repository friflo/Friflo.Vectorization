using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

/// <summary>
/// Uses an event driven approach - DrawFrame() + Shutdown() - instead of running an event loop.<br/>
/// This approach ensures the same renderer can be used on mobile devices or browsers without code changes.<br/>
/// Those platforms only support event driven applications. An event loop would lead to application freeze.
/// </summary>
public partial class RenderTest : IRenderer
{
    // --- IDisposable fields
    protected readonly  GpuBuffer<VertexData>   data;
    protected readonly  PipelineContext         context;
    
    public void OnShutdown()
    {
        context.Dispose();
        data.Dispose();
    }
    
    public RenderTest(Wgpu wgpu)
    {
        this.wgpu = wgpu;
        data        = wgpu.Device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        context     = wgpu.Device.BeginContext();
        rectangle   = data.In(0, 6); // two triangles
    }

    private static readonly VertexData[] Vertices =
    [
        new(new Vector4(-0.5f,  0.5f, 1.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),  // Top-Left
        new(new Vector4(-0.5f, -0.5f, 0.0f, 1), new Vector4(0.0f, 0.0f, 1.0f, 1.0f)),  // Bottom-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f)),  // Bottom-Right
        
        new(new Vector4(-0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f)),  // Top-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f)),  // Bottom-Right
        new(new Vector4( 0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 1.0f, 1.0f, 1.0f))   // Top-Right
    ];

    // --- non-disposable fields
    protected readonly  Wgpu                        wgpu;
    protected readonly  PerfLog                     perfLog             = new();
    protected readonly  InView<VertexData>          rectangle;
    protected           MyUniform                   myUniform           = new() { tint_color = new Vector4(1, 1, 0, 1) };
    protected           Wormhood.Uniforms           wormhood;
    protected readonly  Stopwatch                   stopwatch           = Stopwatch.StartNew();
    protected           WgpuRenderPassDescriptor    renderPassDescriptor= new () { colorAttachments = [ default ] };
    
    
    public void OnWindowChanged(int width, int height)
    {
        renderPassDescriptor.colorAttachments[0] = new WgpuRenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new Color{ r = 0.1, g = 0.1, b = 0.1, a = 1 }
        };
    }
    
    public virtual void OnFrame(int width, int height)
    {
        using var frame = context.BeginFrame(wgpu.Surface);
        if (frame.IsNull) {     // window minimized?
            return;
        }
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        
        using (var pass = frame.BeginRenderPass(renderPassDescriptor))
        {
            myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);
            wormhood.IResolution    = new Vector3(width, height, 1.0f);
            wormhood.ITime          = time;
            
            Wormhood.RenderTunnel(pass, wgpu.Config, wormhood);
            DrawTriangles(pass, wgpu.Config, rectangle, myUniform);
        }
        context.Queue.Submit();
        wgpu.Surface.Present();
    }

    [NoEmit]
	[Shader("shaders/triangle.wgsl")]
    public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
        [Draw]  [BindStorage(0, 0)] InBuffer<VertexData>    triangles,
                [BindUniform(1, 0)] MyUniform               myUniform);
}




[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct VertexData(Vector4 position, Vector4 color)
{
    public Vector4 	position    = position;
    public Vector4 	color       = color;
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct MyUniform
{
    public Vector4 	tint_color;
}

public static partial class Wormhood
{
    [NoEmit]
    [Shader("shaders/raymarcher_no_texture.wgsl")]
    [DrawVertexIndex(3, 1)]
    public static partial void RenderTunnel(RenderPass pass, RenderConfig config,
        [BindUniform(0, 0)] Uniforms    uniforms);
     
    [StructLayout(LayoutKind.Sequential)]
    public struct Uniforms
    {
        public  Vector3     IResolution;
        private float       _pad;       // 16-Byte Alignment for Vector3
        public  float       ITime;
        private Vector3     _pad2;      // fill block for 16 byte alignment
    }
}