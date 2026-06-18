using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public partial class RenderTest : IRenderer
{
    private readonly    Wgpu                    wgpu;
    private readonly    PipelineContext         context;
    private readonly    GpuBuffer<VertexData>   data;
    
    public RenderTest(Wgpu wgpu)
    {
        this.wgpu = wgpu;
        data    = wgpu.Device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        context = wgpu.Device.BeginContext();
    }
    
    public void Shutdown()
    {
        context.Dispose();
        data.Dispose();
    }
    
    private static readonly VertexData[] Vertices =
    [
        new(new Vector4(-0.5f,  0.5f, 1.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 0.0f)),  // Top-Left
        new(new Vector4(-0.5f, -0.5f, 0.0f, 1), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 1.0f)),  // Bottom-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f), new Vector2(1.0f, 1.0f)),  // Bottom-Right
        
        new(new Vector4(-0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 0.0f)),  // Top-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f), new Vector2(1.0f, 1.0f)),  // Bottom-Right
        new(new Vector4( 0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector2(1.0f, 0.0f))   // Top-Right
    ];
    
    private long memoryAllocated;
    
    public void DrawFrame()
    {
        var cur = GC.GetAllocatedBytesForCurrentThread();
        if (cur != memoryAllocated) Console.Out.WriteLine($"{cur -  memoryAllocated} memory used");
        memoryAllocated = GC.GetAllocatedBytesForCurrentThread();
        
        var attachment = new RenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new Color { r = 0.1, g = 0.1, b = 0.1, a = 1 },
            depthSlice  = 0xFFFFFFFF // 0xFFFFFFFF = WGPU_DEPTH_SLICE_UNDEFINED. Prevent wgpu expects 3D Texture
        };
        using var frame = context.BeginFrame(wgpu.Surface);
        if (frame == null) {    // window minimized?
            Thread.Sleep(16);   // prevent CPU consuming 100%
            return;
        }
        using (var pass = frame.Value.BeginRenderPass<MainWorld>(attachment))
        {
            var effect = new MyUniform (new Vector4(1, 1, 0, 1));
            DrawTriangles(pass, data.In(0, 6), effect, wgpu.Config);
            // multiple Draw*() methods can be called here
        }
        // context.Queue.Submit();              // TODO implement Submit()
        context.Queue.ReadBuffers();
        wgpu.Surface.Present();
    }

	// language=file-reference
	[Shader("Shaders/triangle.wgsl")]  // triggers C# source generator to emit method body
    static partial void DrawTriangles(
                        RenderPass<MainWorld>   renderPass,
        [Binding(0, 0)] InBuffer<VertexData>    triangles,
        [Binding(1, 0)] MyUniform               myUniform,
                        RenderConfig            config);
}

public struct MainWorld;

[StructLayout(LayoutKind.Sequential, Size = 48)]
public struct VertexData(Vector4 position, Vector4 color, Vector2 uv)
{
    public Vector4 	position    = position;
    public Vector4 	color       = color;
    public Vector2 	uv          = uv;
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct MyUniform(Vector4 tintColor)
{
    public Vector4 	tint_color = tintColor;
}
