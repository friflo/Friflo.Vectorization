
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

public static class RenderTest
{
    public static bool Running = true;
    
    public struct MainWorld {}
    
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexData(Vector3 position, Vector4 color, Vector2 uv)
    {
        public Vector3 	position    = position;
        public Vector4 	color       = color;
        public Vector2 	uv          = uv;
    }
    
    [Shader<MainWorld>(wgsl: "Shaders/triangle.wgsl")]
	private static void Triangles([Span] VertexData triangles) { }
    
    // draw method - will be generated
    public static void DrawTriangles(RenderPass<MainWorld> pass, GpuBuffer<VertexData> triangles)
	{
		// ... 
	}

    private static readonly VertexData[] Vertices =
    [
        new(new Vector3(-0.5f,  0.5f, 0.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f), new Vector2(0.0f, 0.0f)),
        new(new Vector3(-0.5f, -0.5f, 0.0f), new Vector4(0.0f, 1.0f, 0.0f, 1.0f), new Vector2(0.0f, 1.0f)),
        new(new Vector3( 0.5f, -0.5f, 0.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), new Vector2(1.0f, 1.0f)),
        
        new(new Vector3(-0.5f,  0.5f, 0.0f), new Vector4(1.0f, 0.0f, 0.0f, 1.0f), new Vector2(0.0f, 0.0f)),
        new(new Vector3( 0.5f, -0.5f, 0.0f), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), new Vector2(1.0f, 1.0f)),
        new(new Vector3( 0.5f,  0.5f, 0.0f), new Vector4(1.0f, 1.0f, 0.0f, 1.0f), new Vector2(1.0f, 0.0f))
    ];
    
    public static void Run()
    {
        using var instance  = WgpuInstance.CreateInstance(new InstanceExtras());
        using var adapter   = instance.RequestAdapter(default, null);
        using var device    = adapter.CreateDevice("test");
        
        using var data      = device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        
        using var context   = device.BeginContext();
        
        while (Running)
        {
            using var frame = context.BeginFrame();
            var attachment = new RenderPassColorAttachment {
                loadOp      = LoadOp.Clear,
                storeOp     = StoreOp.Store,
                clearValue  = new Color { r = 0.1, g = 0.1, b = 0.1, a = 1 }
            };
            using var pass = frame.BeginRenderPass<MainWorld>(attachment);
            
            DrawTriangles(pass, data);
        }
    }
}