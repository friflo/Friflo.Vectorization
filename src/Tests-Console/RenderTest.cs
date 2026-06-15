
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using SDL3;

// ReSharper disable ArrangeRedundantParentheses
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedParameter.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable FieldCanBeMadeReadOnly.Global
namespace TestConsole;

public struct MainWorld {}

public static partial class RenderTest
{
    private static bool running = true;

    private static readonly VertexData[] Vertices =
    [
        new(new Vector4(-0.5f,  0.5f, 1.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 0.0f)),  // Top-Left
        new(new Vector4(-0.5f, -0.5f, 0.0f, 1), new Vector4(0.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 1.0f)),  // Bottom-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f), new Vector2(1.0f, 1.0f)),  // Bottom-Right
        
        new(new Vector4(-0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 0.0f, 1.0f, 1.0f), new Vector2(0.0f, 0.0f)),  // Top-Left
        new(new Vector4( 0.5f, -0.5f, 0.0f, 1), new Vector4(0.9f, 0.0f, 0.0f, 1.0f), new Vector2(1.0f, 1.0f)),  // Bottom-Right
        new(new Vector4( 0.5f,  0.5f, 0.0f, 1), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector2(1.0f, 0.0f))   // Top-Right
    ];
    
    private static void InitSDL3(int width, int height, out nint osHandle, out nint osInstance)
    {
        if (!SDL.Init(SDL.InitFlags.Video)) throw new Exception($"SDL3 initialization failed: {SDL.GetError()}");
        
        var windowFlags = SDL.WindowFlags.Hidden;
        if (OperatingSystem.IsMacOS()) {
            windowFlags |= SDL.WindowFlags.Metal | SDL.WindowFlags.HighPixelDensity;
        }
        var window = SDL.CreateWindow("friflo GPU", width, height, windowFlags);
        if (window == IntPtr.Zero)          throw new Exception($"Failed to create window: {SDL.GetError()}");

        var props   = SDL.GetWindowProperties(window);
        if (OperatingSystem.IsWindows()) {
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowWin32HWNDPointer,       IntPtr.Zero);
            osInstance  = SDL.GetPointerProperty(props, SDL.Props.WindowWin32InstancePointer,   IntPtr.Zero);
        } else if (OperatingSystem.IsMacOS()) {
            osHandle    = SDL.GetPointerProperty(props, SDL.Props.WindowCocoaWindowPointer,     IntPtr.Zero);
            osInstance  = 0;
        } else {
            throw new NotImplementedException($"not SDL3 setup code of OS: {RuntimeInformation.OSDescription}");
        }
        SDL.ShowWindow(window);
    }
    
    public static void Run()
    {
        var width  = 1280;
        var height =  720;
        InitSDL3(width, height, out var osHandle, out var osInstance);
        
        // --- setup wgpu-native ---
        using var instance  = WgpuInstance.CreateInstance(new InstanceExtras());
        var surface         = WgpuSurface.CreateFromNativeWindow(instance, osHandle, osInstance);
        using var adapter   = instance.RequestAdapter(default, null);
        using var device    = adapter.CreateDevice("test");
        
        surface.Configure((WgpuDevice)device, width, height);

        var config = WgpuRenderPipelineDescriptor.DefaultRenderPipeline; // 'Default Render Pipeline'
        
        RunLoop(device, surface, config);
    }
    
    private static void RunLoop(GpuDevice device, WgpuSurface surface, RenderPipelineConfig config)
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
        
        while (running)
        {
            while (SDL.PollEvent(out var e)) {
                if ((e.Type == (uint)SDL.EventType.Quit)  ||
                    (e.Type == (uint)SDL.EventType.KeyDown && e.Key.Scancode == SDL.Scancode.Escape)) {
                    running = false;
                }
            }
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