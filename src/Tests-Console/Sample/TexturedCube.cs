using System.Diagnostics;
using System.Numerics;
using Friflo.GPU;
using Friflo.WGPU;
using StbImageSharp;
using TestConsole;

// ReSharper disable ConvertToPrimaryConstructor
namespace Shaders.TexturedCube;

public partial class Renderer : IRenderer
{
    // --- IDisposable fields
    private readonly    GpuTexture          cubeTexture;
    private readonly    GpuSampler          sampler;
    private readonly    GpuBuffer<float>    verticesBuffer;
    private             GpuTexture?         depthTexture;
    
    public void OnShutdown()
    {
        depthTexture?.Dispose();
        verticesBuffer.Dispose();
        sampler.Dispose();
        cubeTexture.Dispose();
    }
    
    public Renderer(Wgpu wgpu)
    {
        this.wgpu   = wgpu;
        var device  = wgpu.Device;
        
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L112
        using var stream = typeof(SdlWindow).Assembly.GetManifestResourceStream( "Tests-Console.Assets.img.Di-3d.png");
        var image   = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        cubeTexture = device.CreateTexture(new GpuTextureDescriptor { label = "Di-3d.png", 
            size    = [image.Width, image.Height],
            format  = TextureFormat.RGBA8Unorm,
            usage   = TextureUsage.TextureBinding | TextureUsage.CopyDst
        });
        sampler = device.CreateSampler(new GpuSamplerDescriptor {
            magFilter = FilterMode.Linear,
            minFilter = FilterMode.Linear
        });
        cubeTexture.Write(image.Data, bytesPerRow: image.Width * 4, rowsPerImage: image.Height);
        
        textureView = cubeTexture.CreateView();

        
        // --- Cube Vertex Buffer Config
        verticesBuffer = device.CreateBuffer(Cube.cubeVertexArray, "verticesBuffer", BufferProfile.StaticIn, BufferType.Vertex);
        verticesBuffer.In().Write();
        
        var desc = wgpu.Config.Descriptor;
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L49
        desc.VertexState.buffers = [
            new GpuVertexBufferLayout {    // buffers[0]  <-  referenced by [VertexBuffer(0)]   (slot: 0)
                arrayStride = Cube.cubeVertexSize,
                attributes = [
                    new GpuVertexAttribute {
                        shaderLocation = 0, // basic.vert.wgsl:  @location(0) position : vec4f
                        offset = Cube.cubePositionOffset,
                        format = VertexFormat.Float32x4
                    },
                    new GpuVertexAttribute {
                        shaderLocation = 1, // basic.vert.wgsl:  @location(1) uv : vec2f
                        offset = Cube.cubeUVOffset,
                        format = VertexFormat.Float32x2
                    },
                ]
        }];
        desc.PrimitiveState = new GpuPrimitiveState {
            topology    = PrimitiveTopology.TriangleList,
            cullMode    = CullMode.Back
        };
        desc.DepthStencilState = new GpuDepthStencilState {
            depthWriteEnabled   = true,
            depthCompare        = CompareFunction.Less,
            format              = TextureFormat.Depth24Plus
        };
        config = desc.CreateConfig("Cube Vertex Config");
    }

    // --- non-disposable fields
    private   readonly  Wgpu                    wgpu;
    private   readonly  RenderConfig            config;
    private   readonly  GpuTextureView          textureView;
    private   readonly  PerfLog                 perfLog             = new();
    private             Uniforms                uniforms;
    private   readonly  Stopwatch               stopwatch           = Stopwatch.StartNew();
    private             GpuRenderPassDescriptor renderPassDescriptor= new() { colorAttachments = [ default ] };

    
    public void OnWindowChanged(int width, int height)
    {
        depthTexture?.Dispose();
        depthTexture = wgpu.Device.CreateTexture(new GpuTextureDescriptor {
            size    = [width, height],
            format  = TextureFormat.Depth24Plus,
            usage   = TextureUsage.RenderAttachment
        });
        
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L146
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            view        = default,  // Assigned later for each frame
            clearValue  = [0.5, 0.5, 0.5, 1],
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store
        };
        renderPassDescriptor.depthStencilAttachment = new GpuRenderPassDepthStencilAttachment {
            view            = depthTexture.CreateView(),
            depthClearValue = 1,
            depthLoadOp     = LoadOp.Clear,
            depthStoreOp    = StoreOp.Store
        };
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L168
    private static Matrix4x4 GetTransformationMatrix(GpuExtent3D size, float time)
    {
        var proj = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, size.AspectRatio, 1f, 100f);
        var view = Matrix4x4.CreateRotationX(MathF.Sin(time)) * Matrix4x4.CreateRotationY(MathF.Cos(time))
                 * Matrix4x4.CreateTranslation(0, 0, -4f);
        return view * proj;
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L179
    public void OnFrame(in RenderTarget target)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = target.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        uniforms.modelViewProjectionMatrix = GetTransformationMatrix(target.TargetSize, time);
        
        using var pass = target.BeginRenderPass(renderPassDescriptor);
        
        RenderTexturedCube(pass, config, uniforms, sampler, textureView, verticesBuffer.In());
    }
    
	[Shader("~/shaders/basic.vert.wgsl",                                vertex:   "main")]
	[Shader("~/shaders/texturedCube/sampleTextureMixColor.frag.wgsl",   fragment: "main")]
    private static partial void RenderTexturedCube(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in Uniforms     uniforms,
        [Map(0, 1)] [sampler]                   GpuSampler      smoothFilter,
        [Map(0, 2)] [texture_2d(ST.f32)]        GpuTextureView  material,
                    [VertexBuffer(0)] [Draw]    InBuffer<float> vertices);

}
