using System.Diagnostics;
using System.Numerics;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public partial class InstancedCube : IRenderer
{
    // --- IDisposable fields
    private readonly    PipelineContext         context;
    private readonly    GpuBuffer<float>        verticesBuffer;
    private             GpuTexture?             depthTexture;
    private readonly    GpuBuffer<Matrix4x4>    mvpMatricesData;
    
    public void OnShutdown()
    {
        depthTexture?.Dispose();
        verticesBuffer.Dispose();
        context.Dispose();
    }
    
    public InstancedCube(Wgpu wgpu)
    {
        this.wgpu   = wgpu;
        var device  = wgpu.Device;
        context     = device.BeginContext();
        
        // --- Cube Vertex Buffer Config
        verticesBuffer = wgpu.Device.CreateBuffer(Cube.cubeVertexArray, "verticesBuffer", BufferProfile.StaticIn, BufferType.Vertex);
        verticesBuffer.In().Write(context);
        
        mvpMatricesData = wgpu.Device.CreateBuffer(mvpMatrices, "mvpMatricesData", BufferProfile.StaticIn, BufferType.Uniform);
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
        config = desc.CreateConfig("Cube Vertex Config");
    }

    // --- non-disposable fields
    private   readonly  Wgpu                        wgpu;
    private   readonly  RenderConfig                config;
    private   readonly  PerfLog                     perfLog             = new();
    private   const     int                         xCount          = 4;
    private   const     int                         yCount          = 4;
    private   const     int                         numInstances    = xCount * yCount;
    private   readonly  Matrix4x4[]                 modelMatrices   = new Matrix4x4[numInstances];
    private   readonly  Matrix4x4[]                 mvpMatrices     = new Matrix4x4[numInstances];
    private   readonly  Matrix4x4                   viewMatrix      = Matrix4x4.CreateTranslation(new Vector3(0, 0, -12));
    private   readonly  Stopwatch                   stopwatch       = Stopwatch.StartNew();
    private             WgpuRenderPassDescriptor    renderPassDescriptor= new() { colorAttachments = [ default ] };

    
    public void OnWindowChanged(int width, int height)
    {
        depthTexture?.Dispose(); // create new depthTexture with different width & height
        depthTexture = wgpu.Device.CreateTexture(new GpuTextureDescriptor {
            size    = [width, height],
            format  = TextureFormat.Depth24Plus,
            usage   = TextureUsage.RenderAttachment
        });
        
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/instancedCube/main.ts#L173
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
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/instancedCube/main.ts#L148
    private void UpdateTransformationMatrix(float width, float height, float now)
    {
        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, width / height, 1f, 100f);
        int i = 0;
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
    public void OnFrame(int width, int height)
    {
        using var frame = context.BeginFrame(wgpu.Surface);
        if (frame.IsNull) {     // window minimized?
            return;
        }
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        UpdateTransformationMatrix(width, height, time);
        
        using (var pass = frame.BeginRenderPass(renderPassDescriptor))
        {
            RenderCubes(pass, config, verticesBuffer.In(), mvpMatricesData.In().Write());
        }
        context.Queue.Submit();    
        wgpu.Surface.Present();
    }
    
	[VertexShader  ("shaders/instanced.vert.wgsl",              vert: "main")]
	[FragmentShader("shaders/vertexPositionColor.frag.wgsl",    frag: "main")]
    public static partial void RenderCubes(RenderPass pass, RenderConfig config,
        [VertexBuffer(0)]           InBuffer<float>     verticesBuffer,
        [BindUniform     (0, 0)]    InBuffer<Matrix4x4> modelMatricesData);
}
