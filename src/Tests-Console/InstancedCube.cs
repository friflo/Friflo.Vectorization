using System.Diagnostics;
using System.Numerics;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;


// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public partial class InstancedCube : IRenderer
{
    // --- IDisposable fields
    private readonly    GpuBuffer<float>        verticesBuffer;
    private             GpuTexture?             depthTexture;
    private readonly    GpuBuffer<Matrix4x4>    mvpMatricesData;
    
    private readonly    bool useUniformBuffer = true; // true == original WebGPU JS example 
    // true:  Uniform Buffer - WebGPU standard limit (max 64 KiB -> max 1,024 instances / 32 x 32 grid)
    // false: Storage Buffer - Supports massive data loads (min 128 MiB -> over 2 million instances)
    //        Tip for max FPS: Update transformations via Compute Shader directly on GPU to eliminate CPU loop math and PCIe transfer of .Write().
    
    public void OnShutdown()
    {
        mvpMatricesData.Dispose();
        depthTexture?.Dispose();
        verticesBuffer.Dispose();
    }
    
    public InstancedCube(Wgpu wgpu)
    {
        this.wgpu   = wgpu;
        var device  = wgpu.Device;
        
        // --- Cube Vertex Buffer Config
        verticesBuffer = device.CreateBuffer(Cube.cubeVertexArray, "verticesBuffer", BufferProfile.StaticIn, BufferType.Vertex);
        verticesBuffer.In().Write();
        
        var bufferType  = useUniformBuffer ? BufferType.Uniform : BufferType.Storage;
        mvpMatricesData = device.CreateBuffer<Matrix4x4>(numInstances, default, "mvpMatricesData", BufferProfile.StaticIn, bufferType);
        const float step = 4.0f;

        // Initialize the matrix data for every instance.
        int m = 0;
        for (var x = 0; x < xCount; x++) {
            for (var y = 0; y < yCount; y++) {
                modelMatrices[m] = Matrix4x4.CreateTranslation(new Vector3(
                    step * (x - xCount / 2f + 0.5f),
                    step * (y - yCount / 2f + 0.5f),
                    0f
                ));
                m++;
            }
        }
        
        var desc = wgpu.Config.Descriptor;
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/instancedCube/main.ts#L49
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
    private   const     int                     xCount              = 4; // 32  1400
    private   const     int                     yCount              = 4; // 32  1400
    private   const     int                     numInstances        = xCount * yCount;
    private   readonly  Matrix4x4[]             modelMatrices       = new Matrix4x4[numInstances];
    private   readonly  Matrix4x4               viewMatrix          = Matrix4x4.CreateTranslation(new Vector3(0, 0, -12));
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
        
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/instancedCube/main.ts#L173
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
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/instancedCube/main.ts#L148
    private void UpdateTransformationMatrix(float width, float height, float now)
    {
        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, width / height, 1f, 100f);
        int i = 0;
        var mvpMatrices = mvpMatricesData.In().Span;
        for (int x = 0; x < xCount; x++) {
            for (int y = 0; y < yCount; y++) {
                var rawAxis     = new Vector3(MathF.Sin((x + 0.5f) * now), MathF.Cos((y + 0.5f) * now), 0f);
                var axis        = Vector3.Normalize(rawAxis);   // JS: mat4.rotate() normalize the axis internally
                var modelMatrix = Matrix4x4.CreateFromAxisAngle(axis, 1f) * modelMatrices[i];
                mvpMatrices[i]  = modelMatrix * viewMatrix * projectionMatrix;
                i++;
            }
        }
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/instancedCube/main.ts#L192
    public void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        UpdateTransformationMatrix(frame.Width, frame.Height, time);
        
        using var pass = frame.BeginRenderPass(renderPassDescriptor);
        
        if (useUniformBuffer) {
            RenderCubes(pass, config, verticesBuffer.In(), mvpMatricesData.In().Write());
        } else {
            RenderCubesStorage(pass, config, verticesBuffer.In(), mvpMatricesData.In().Write());
        }
    }
    
	[Shader("~/shaders/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    private static partial void RenderCubes(RenderPass pass, RenderConfig config,
        [Draw]          [VertexBuffer(0)]       InBuffer<float>     verticesBuffer,
        [DrawInstance]  [Uniform][Bind(0, 0)]   InBuffer<Matrix4x4> mvpMatrices);
    
    // Alternative Shader method with [BindStorage(0, 0)] to use a Storage Buffer
	[Shader("~/shaders/instanced.storage.vert.wgsl",      vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    private static partial void RenderCubesStorage(RenderPass pass, RenderConfig config,
        [Draw]          [VertexBuffer(0)]       InBuffer<float>     verticesBuffer,
        [DrawInstance]  [Storage][Bind(0, 0)]   InBuffer<Matrix4x4> mvpMatrices);
}
