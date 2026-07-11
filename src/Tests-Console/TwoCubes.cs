using System.Diagnostics;
using System.Numerics;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public partial class TwoCubes : IRenderer
{
    // --- IDisposable fields
    private readonly    GpuBuffer<float>    verticesBuffer;
    private             GpuTexture?         depthTexture;
    
    public void OnShutdown()
    {
        depthTexture?.Dispose();
        verticesBuffer.Dispose();
    }
    
    public TwoCubes(Wgpu wgpu)
    {
        this.wgpu   = wgpu;
        
        // --- Cube Vertex Buffer Config
        verticesBuffer = wgpu.Device.CreateBuffer(Cube.cubeVertexArray, "verticesBuffer", BufferProfile.StaticIn, BufferType.Vertex);
        verticesBuffer.In().Write();
        
        var desc = wgpu.Config.Descriptor;
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/twoCubes/main.ts#L49
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
    private   readonly  PerfLog                 perfLog             = new();
    private   readonly  Matrix4x4               modelMatrix1        = Matrix4x4.CreateTranslation(new Vector3(-2, 0, 0));
    private   readonly  Matrix4x4               modelMatrix2        = Matrix4x4.CreateTranslation(new Vector3( 2, 0, 0));
    private             Matrix4x4               modelViewProjectionMatrix1;
    private             Matrix4x4               modelViewProjectionMatrix2;
    private   readonly  Matrix4x4               viewMatrix          = Matrix4x4.CreateTranslation(new Vector3(0, 0, -7));
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
        
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/twoCubes/main.ts#L140
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
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/twoCubes/main.ts#L171
    private void UpdateTransformationMatrix(float width, float height, float now)
    {
        var tmpMat41 = Matrix4x4.CreateFromAxisAngle(new Vector3(MathF.Sin(now), MathF.Cos(now), 0), 1f) * modelMatrix1;
        var tmpMat42 = Matrix4x4.CreateFromAxisAngle(new Vector3(MathF.Cos(now), MathF.Sin(now), 0), 1f) * modelMatrix2;
        
        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, width / height, 1f, 100f);
        
        modelViewProjectionMatrix1 = tmpMat41 * viewMatrix * projectionMatrix;
        modelViewProjectionMatrix2 = tmpMat42 * viewMatrix * projectionMatrix;
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/twoCubes/main.ts#L191
    public void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        UpdateTransformationMatrix(frame.Width, frame.Height, time);
        
        using var pass = frame.BeginRenderPass(renderPassDescriptor);
        
        RenderCube(pass, config, verticesBuffer.In(), modelViewProjectionMatrix1);
        RenderCube(pass, config, verticesBuffer.In(), modelViewProjectionMatrix2);
    }
    
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    private static partial void RenderCube(RenderPass pass, RenderConfig config,
        [Draw]  [VertexBuffer(0)]   InBuffer<float> verticesBuffer,
                [BindUniform(0, 0)] in Matrix4x4    modelViewProjectionMatrix);
}
