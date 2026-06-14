
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable FieldCanBeMadeReadOnly.Global
namespace TestConsole;

public struct MainWorld {}

public static partial class RenderTest
{
    public static bool Running = true;

    private static readonly VertexData[] Vertices =
    [
        new(new Vector4(-0.5f,  0.5f, 1.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 0.0f)),  // Top-Left
        new(new Vector4(-0.5f, -0.5f, 0.0f, 1), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 1.0f)),  // Bottom-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f), new Vector2(1.0f, 1.0f)),  // Bottom-Right
        
        new(new Vector4(-0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 0.0f)),  // Top-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f), new Vector2(1.0f, 1.0f)),  // Bottom-Right
        new(new Vector4( 0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector2(1.0f, 0.0f))   // Top-Right
    ];
    
    public static void Run()
    {
        using var instance  = WgpuInstance.CreateInstance(new InstanceExtras());
        using var adapter   = instance.RequestAdapter(default, null);
        using var device    = adapter.CreateDevice("test");
        
        var hInstance   = Windowing.GetModuleHandleW(null);
        var hwnd        = Windowing.CreateWindowExW(0, "Static", "wgpu", 0x10CF0000, 100, 100, 1280, 720, 0, 0, hInstance, 0);
        
        var surface     = WgpuSurface.CreateFromHwnd(instance, hwnd, hInstance);
        surface.Configure((WgpuDevice)device, 1280, 720);
        
        var desc = new RenderConfigDescriptor();
        var config = desc.GetConfig();
        
        RunLoop(device, surface, config);
    }
    
    private static void RunLoop(GpuDevice device, WgpuSurface surface, RenderConfig config)
    {
        using var data      = device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        using var context   = device.BeginContext();
        context.EnableTraces = true;
        
        var attachment = new RenderPassColorAttachment {
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store,
            clearValue  = new Color { r = 0.1, g = 0.1, b = 0.1, a = 1 },
            depthSlice  = 0xFFFFFFFF // 0xFFFFFFFF = WGPU_DEPTH_SLICE_UNDEFINED. Prevent wgpu expects 3D Texture
        };
        
        while (Running)
        {
            using var frame = context.BeginFrame(surface);
            
            using (var pass = frame.BeginRenderPass<MainWorld>(attachment))
            {
                DrawTriangles(pass, data.In(), config);
                // multiple Draw*() methods can be called here
            }
            // context.Queue.Submit();              // TODO implement Submit()
            context.Queue.ReadBuffers();
            surface.Present();
        }
    }
    
    /// blueprint method generates:  <see cref="DrawTriangles"/>
    [Shader<MainWorld>(wgsl: "Shaders/triangle.wgsl")]
	private static void Triangles([Span] VertexData triangles) { }
}

[StructLayout(LayoutKind.Sequential, Size = 48)]
public struct VertexData(Vector4 position, Vector4 color, Vector2 uv)
{
    public Vector4 	position    = position;
    public Vector4 	color       = color;
    public Vector2 	uv          = uv;
}