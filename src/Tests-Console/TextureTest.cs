using System.Diagnostics;
using System.Numerics;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using StbImageSharp;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

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
        this.wgpu = wgpu;
        var device = wgpu.Device;
        data        = device.CreateBuffer(Vertices, "data", BufferProfile.InOut);
        context     = device.BeginContext();
        rectangle   = data.In(0, 6); // two triangles
        
        using var stream = typeof(SdlWindow).Assembly.GetManifestResourceStream( "Tests-Console.Assets.img.Di-3d.png");
        var image   = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        texture2D   = device.CreateTexture2D(image.Width, image.Height, new TextureDescriptor {
            format      = TextureFormat.RGBA8Unorm,
            usage       = TextureUsage_TextureBinding | TextureUsage_CopyDst | TextureUsage_RenderAttachment    // todo  use enums from new WGPU binding
        });
        sampler     = device.CreateSampler(new SamplerDescriptor {
            magFilter   = FilterMode.Linear,
            minFilter   = FilterMode.Linear,
        });
        var layout = new TexelCopyBufferLayout { bytesPerRow = (uint)image.Width * 4, rowsPerImage = (uint)image.Height };
        texture2D.Write(new TexelCopyTextureInfo(), image.Data, layout);
        
        texture_2d  = texture2D.texture_2d<float>();
        var temp    = texture2D.texture_2d<float>();
        
        var tempHandle = texture_2d.Handle;
    }

    readonly texture_2d<float> texture_2d;
    
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
            RenderSubmarine(pass, texture_2d, sampler);
        }
        context.Queue.Submit();
        wgpu.Surface.Present();
    }

	// language=file-reference
	[Shader("Shaders/triangle.wgsl")]  // triggers C# source generator to emit method body
    protected static void RenderSubmarine(
                            RenderPass<MainWorld>   renderPass,
        [BindTexture(0, 0)] texture_2d<float>       material,
        [BindSampler(0, 1)] GpuSampler              smoothFilter) { }
}
