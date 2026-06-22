using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using StbImageSharp;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public class TextureTest : IRenderer
{
    private readonly    Wgpu                    wgpu;
    private readonly    PipelineContext         context;
    private readonly    GpuBuffer<VertexData>   data;
    private readonly    GpuTexture2D            texture2D;
    private readonly    GpuSampler              sampler;
    
    
    public TextureTest(Wgpu wgpu)
    {
        this.wgpu   = wgpu;
        var device  = wgpu.Device;
        data        = device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        context     = device.BeginContext();
        rectangle   = data.In(0, 6); // two triangles
        
        using var stream = typeof(SdlWindow).Assembly.GetManifestResourceStream( "Tests-Console.Assets.img.Di-3d.png");
        var image   = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        texture2D   = device.CreateTexture2D(image.Width, image.Height, TextureFormat.RGBA8Unorm,
                        TextureUsage.TextureBinding | TextureUsage.CopyDst | TextureUsage.RenderAttachment, "Di-3d.png");
        sampler     = device.CreateSampler(FilterMode.Linear, FilterMode.Linear, "Sampler");
        texture2D.Write(image.Data, bytesPerRow: image.Width * 4, rowsPerImage: image.Height);
        
        textureView = texture2D.texture_2d<float>();
        var temp    = texture2D.texture_2d<float>();

        var tempHandle = textureView.Handle;
    }

    private readonly texture_2d<float> textureView;
    
    public void Shutdown()
    {
        sampler.Dispose();
        texture2D.Dispose();
        context.Dispose();
        data.Dispose();
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
    

    protected readonly  PerfLog                     perfLog     = new();
    protected           Uniforms                    uniforms    = new();
    protected readonly  InView<VertexData>          rectangle;
    protected           MyUniform                   myUniform   = new() { tint_color = new Vector4(1, 1, 0, 1) };
    protected readonly  Stopwatch                   stopwatch   = Stopwatch.StartNew();
    protected readonly  RenderPassColorAttachment   attachment  = new() {
        loadOp      = LoadOp.Clear,
        storeOp     = StoreOp.Store,
        clearValue  = new Color{ r = 0.1, g = 0.1, b = 0.1, a = 1 },
        depthSlice  = 0xFFFFFFFF // 0xFFFFFFFF = WGPU_DEPTH_SLICE_UNDEFINED. Prevent wgpu expects 3D Texture
    };
    
    public void DrawFrame()
    {
        using var frame = context.BeginFrame(wgpu.Surface);
        if (frame.IsNull) {     // window minimized?
            return;
        }
        perfLog.Trace(5000);
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        
        using (var pass = frame.BeginRenderPass<MainWorld>(attachment, wgpu.Config))
        {
            myUniform.tint_color.Z  = 0.5f * (MathF.Sin(time * 5) + 1f);

            RenderTest.DrawTriangles(pass, rectangle, myUniform);
            RenderSubmarine(pass, uniforms, sampler, textureView);
        }
        context.Queue.Submit();
        wgpu.Surface.Present();
    }
    
    // planned: [Shader()] triggers C# source generator to emit method body
	[VertexShader  ("shaders/basic.vert.wgsl",                  vert: "main")]
	[FragmentShader("shaders/sampleTextureMixColor.frag.wgsl",  frag: "main")]
    protected static void RenderSubmarine(
                            RenderPass<MainWorld>   renderPass,
        [BindUniform(0, 0)] in Uniforms             uniforms,
        [BindSampler(0, 1)] GpuSampler              smoothFilter,
        [BindTexture(0, 2)] texture_2d<float>       material) { }


    [StructLayout(LayoutKind.Sequential)]
    protected struct Uniforms {
        Matrix4x4   modelViewProjectionMatrix;
    }
}
