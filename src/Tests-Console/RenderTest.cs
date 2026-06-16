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

public partial struct Renderer : IDisposable
{
    private WgpuInstance            instance;
    public  WgpuSurface             surface;
    public  TextureFormat           swapChainFormat;
    private WgpuAdapter             adapter;
    public  GpuDevice               device;
    private RenderPipelineConfig    config;
    private PipelineContext         context;
    private GpuBuffer<VertexData>   data;
    
    public void Dispose()
    {
        data.Dispose();
        context.Dispose();
        device.Dispose();
        adapter.Dispose();
        instance.Dispose();
    }
    
    public static Renderer Create(SdlWindow window)
    {
        var width  = 1280;
        var height =  720;
        window.InitSDL3(width, height, out var osHandle, out var osInstance);
        
        var renderer = new Renderer();
        
        renderer.instance   = WgpuInstance.CreateInstance(new InstanceExtras());
        renderer.surface    = WgpuSurface.CreateFromNativeWindow(renderer.instance, osHandle, osInstance);
        renderer.adapter    = renderer.instance.RequestAdapter(default, null);
        renderer.device     = renderer.adapter.CreateDevice("test");
        
        renderer.config = WgpuRenderPipelineDescriptor.DefaultRenderPipeline;
        
        renderer.swapChainFormat = renderer.config.Descriptor.FragmentState!.Value.targets[0].format;  // surface.GetSwapChainFormat(adapter);
        
        renderer.data       = renderer.device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        renderer.context    = renderer.device.BeginContext();
        
        return renderer;
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
