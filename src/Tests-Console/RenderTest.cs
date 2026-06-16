using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable InconsistentNaming
namespace TestConsole;

public partial class RendererTest : IDisposable
{
    private readonly    WgpuInstance            instance;
    public  readonly    WgpuSurface             surface;
    public  readonly    TextureFormat           swapChainFormat;
    public  readonly    CompositeAlphaMode      alphaMode;
    private readonly    WgpuAdapter             adapter;
    public  readonly    GpuDevice               device;
    private readonly    RenderPipelineConfig    config;
    private readonly    PipelineContext         context;
    private readonly    GpuBuffer<VertexData>   data;
    
    public void Dispose()
    {
        data.Dispose();
        context.Dispose();
        device.Dispose();
        adapter.Dispose();
        instance.Dispose();
    }
    
    public RendererTest(SdlWindow window)
    {
        window.InitSDL3(1280, 720, out var osHandle, out var osInstance);
        
        instance    = WgpuInstance.CreateInstance(new InstanceExtras());
        surface     = WgpuSurface.CreateFromNativeWindow(instance, osHandle, osInstance);
        adapter     = instance.RequestAdapter(default, null);
        device      = adapter.CreateDevice("test");
        
        var fragmentState   = surface.GetPreferredFragmentState(adapter, true, out alphaMode);
        swapChainFormat     = fragmentState.targets[0].format;
        var desc            = new WgpuRenderPipelineDescriptor { FragmentState = fragmentState };
        config              = desc.CreateConfig("render config");
        
        data    = device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        context = device.BeginContext();
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
    
    public void DrawFrame()
    {
        var attachment = new RenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new Color { r = 0.1, g = 0.1, b = 0.1, a = 1 },
            depthSlice  = 0xFFFFFFFF // 0xFFFFFFFF = WGPU_DEPTH_SLICE_UNDEFINED. Prevent wgpu expects 3D Texture
        };
        using var frame = context.BeginFrame(surface);
        if (frame == null) {    // window minimized?
            Thread.Sleep(16);   // prevent CPU consuming 100%
            return;
        }
        using (var pass = frame.Value.BeginRenderPass<MainWorld>(attachment))
        {
            DrawTriangles(pass, data.In(), config);
            // multiple Draw*() methods can be called here
        }
        // context.Queue.Submit();              // TODO implement Submit()
        context.Queue.ReadBuffers();
        surface.Present();
    }
    
    /// blueprint method generates:  <see cref="DrawTriangles"/>
    [Shader<MainWorld>(wgsl: "Shaders/triangle.wgsl")]
	private static void Triangles([Span] VertexData triangles) { }
}

public struct MainWorld;

[StructLayout(LayoutKind.Sequential, Size = 48)]
public struct VertexData(Vector4 position, Vector4 color, Vector2 uv)
{
    public Vector4 	position    = position;
    public Vector4 	color       = color;
    public Vector2 	uv          = uv;
}
