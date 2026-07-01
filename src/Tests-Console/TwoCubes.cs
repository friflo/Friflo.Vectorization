using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public partial class TwoCubes : IRenderer
{
    // --- IDisposable fields
    private readonly    PipelineContext     context;
    private readonly    GpuBuffer<float>    verticesBuffer;
    private             GpuTexture?         depthTexture;
    
    public void OnShutdown()
    {
        depthTexture?.Dispose();
        verticesBuffer.Dispose();
        context.Dispose();
    }
    
    public TwoCubes(Wgpu wgpu)
    {
        this.wgpu   = wgpu;
        var device  = wgpu.Device;
        context     = device.BeginContext();
        
        // --- Cube Vertex Buffer Config
        verticesBuffer = wgpu.Device.CreateBuffer(Cube.cubeVertexArray, "verticesBuffer", BufferProfile.StaticIn, BufferType.Vertex);
        verticesBuffer.In().Write(context);
        
        var desc = wgpu.Config.Descriptor;
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L49
        desc.VertexState.buffers = [
            new WgpuVertexBufferLayout {    // buffers[0]  <-  referenced by [VertexBuffer(0)]   (slot: 0)
                arrayStride = Cube.cubeVertexSize,
                attributes = [
                    new WgpuVertexAttribute {
                        shaderLocation = 0, // basic.vert.wgsl:  @location(0) position : vec4f
                        offset = Cube.cubePositionOffset,
                        format = VertexFormat.Float32x4
                    },
                    new WgpuVertexAttribute {
                        shaderLocation = 1, // basic.vert.wgsl:  @location(1) uv : vec2f
                        offset = Cube.cubeUVOffset,
                        format = VertexFormat.Float32x2
                    },
                ]
        }];
        desc.PrimitiveState = new WgpuPrimitiveState {
            topology    = PrimitiveTopology.TriangleList,
            cullMode    = CullMode.Back
        };
        desc.DepthStencilState = new WgpuDepthStencilState {
            depthWriteEnabled   = true,
            depthCompare        = CompareFunction.Less,
            format              = TextureFormat.Depth24Plus
        };
        vertexConfig = desc.CreateConfig("Cube Vertex Config");
    }

    // --- non-disposable fields
    private   readonly  Wgpu                        wgpu;
    private   readonly  RenderConfig                vertexConfig;
    private   readonly  PerfLog                     perfLog             = new();
    private             Uniforms                    uniforms;
    private   readonly  Stopwatch                   stopwatch           = Stopwatch.StartNew();
    private             WgpuRenderPassDescriptor    renderPassDescriptor= new() { colorAttachments = [ default ] };

    
    public void OnWindowChanged(int width, int height)
    {
        depthTexture?.Dispose(); // create new depthTexture with different width & height
        depthTexture = wgpu.Device.CreateTexture(new GpuTextureDescriptor {
            size    = [width, height],
            format  = TextureFormat.Depth24Plus,
            usage   = TextureUsage.RenderAttachment
        });
        
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L146
        renderPassDescriptor.colorAttachments[0] = new WgpuRenderPassColorAttachment {
            view        = default,  // Assigned later for each frame
            clearValue  = new Color{ r = 0.5, g = 0.5, b = 0.5, a = 1 },
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store
        };
        renderPassDescriptor.depthStencilAttachment = new WgpuRenderPassDepthStencilAttachment {
            view            = depthTexture.CreateView(),
            depthClearValue = 1,
            depthLoadOp     = LoadOp.Clear,
            depthStoreOp    = StoreOp.Store
        };
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L168
    private static Matrix4x4 GetTransformationMatrix(float width, float height, float time)
    {
        var proj = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, width / height, 1f, 100f);
        var view = Matrix4x4.CreateRotationX(MathF.Sin(time)) * Matrix4x4.CreateRotationY(MathF.Cos(time))
                 * Matrix4x4.CreateTranslation(0, 0, -4f);
        return view * proj;
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/texturedCube/main.ts#L179
    public void OnFrame(int width, int height)
    {
        using var frame = context.BeginFrame(wgpu.Surface);
        if (frame.IsNull) {     // window minimized?
            return;
        }
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        uniforms.modelViewProjectionMatrix = GetTransformationMatrix(width, height, time);
        
        using (var pass = frame.BeginRenderPass(renderPassDescriptor))
        {
            RenderCube(pass, vertexConfig, verticesBuffer.In(), uniforms);
        }
        context.Queue.Submit();
        wgpu.Surface.Present();
    }
    
	[VertexShader  ("shaders/basic.vert.wgsl",                  vert: "main")]
	[FragmentShader("shaders/vertexPositionColor.frag.wgsl",    frag: "main")]
    protected static partial void RenderCube(RenderPass pass, RenderConfig config,
        [VertexBuffer(0)]           InBuffer<float> verticesBuffer,
        [BindUniform     (0, 0)]    in Uniforms     uniforms);


    [StructLayout(LayoutKind.Sequential)]
    protected struct Uniforms {
        public Matrix4x4   modelViewProjectionMatrix;
    }
}
