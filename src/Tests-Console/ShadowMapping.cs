using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

// ReSharper disable ConvertToPrimaryConstructor
namespace TestConsole;

public partial class ShadowMapping : IRenderer
{
    // --- IDisposable fields
    private readonly    GpuBuffer<Vector3>  vertexBuffer;
    private readonly    GpuBuffer<ushort>   indexBuffer;
    private readonly    GpuTexture          shadowDepthTexture;
    
    private             GpuTexture?         depthTexture;
    
    public void OnShutdown()
    {
        depthTexture?.Dispose();
        shadowDepthTexture.Dispose();
        indexBuffer.Dispose();
        vertexBuffer.Dispose();
    }
    
    public ShadowMapping(Wgpu wgpu)
    {
        this.wgpu   = wgpu;
        var device  = wgpu.Device;
        
        var mesh = StanfordDragon.LoadMeshAsync().Result;
        
        // JS example:  https://github.com/EmilSV/Webgpusharp-examples/blob/main/GraphicsTechniques/ShadowMapping/Program.cs#L78
        // Create the model vertex buffer.
        vertexBuffer = device.CreateBuffer<Vector3>(2 * mesh.positions.Length, default, "vertexBuffer", BufferProfile.StaticIn, BufferType.Vertex);
        var vertexMapping = vertexBuffer.In().Write().Span;
        for (int i = 0; i < mesh.positions.Length; ++i) {
            vertexMapping[2 * i]      = mesh.positions[i];
            vertexMapping[2 * i + 1]  = mesh.normals[i];
        }
        
        // Create the model index buffer.
        indexBuffer = device.CreateBuffer<ushort>(3 * mesh.triangles.Length, 0, "indexBuffer", BufferProfile.StaticIn, BufferType.Index);
        var indexMapping = indexBuffer.In().Write().Span;
        for (int i = 0; i < mesh.triangles.Length; i++) {
            indexMapping[3 * i + 0] = (ushort)mesh.triangles[i].X;
            indexMapping[3 * i + 1] = (ushort)mesh.triangles[i].Y;
            indexMapping[3 * i + 2] = (ushort)mesh.triangles[i].Z;
        }
        
        // Create the depth texture for rendering/sampling the shadow map.
        const int shadowDepthTextureSize = 1024;
        shadowDepthTexture = device.CreateTexture(new GpuTextureDescriptor {
          size      = [shadowDepthTextureSize, shadowDepthTextureSize, 1],
          usage     = TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
          format    = TextureFormat.Depth32Float
        });
        shadowDepthTextureView = shadowDepthTexture.CreateView();
        
        
        var desc = wgpu.Config.Descriptor;
        // Create some common descriptors used for both the shadow pipeline
        // and the color rendering pipeline.
        desc.VertexState.buffers = [
            new GpuVertexBufferLayout {
                arrayStride = Marshal.SizeOf<Vector3>() * 2,
                attributes = [
                    new GpuVertexAttribute {
                        // position
                        shaderLocation  = 0,
                        offset          = 0,
                        format          = VertexFormat.Float32x4
                    },
                    new GpuVertexAttribute {
                        // normal
                        shaderLocation  = 1,
                        offset          = Marshal.SizeOf<Vector3>(),
                        format          = VertexFormat.Float32x2
                    },
                ]
        }];
        desc.PrimitiveState = new GpuPrimitiveState {
            topology    = PrimitiveTopology.TriangleList,
            cullMode    = CullMode.Back
        };

        config = desc.CreateConfig("ShadowMapping Config");
    }

    // --- non-disposable fields
    private   readonly  GpuTextureView          shadowDepthTextureView;
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
        depthTexture?.Dispose(); // create new depthTexture with different width & height
        depthTexture = wgpu.Device.CreateTexture(new GpuTextureDescriptor {
            size    = [width, height],
            format  = TextureFormat.Depth24Plus,
            usage   = TextureUsage.RenderAttachment
        });
        
        // JS example:  ...
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
    
    // JS example:  ...
    private void UpdateTransformationMatrix(float width, float height, float now)
    {
        var tmpMat41 = Matrix4x4.CreateFromAxisAngle(new Vector3(MathF.Sin(now), MathF.Cos(now), 0), 1f) * modelMatrix1;
        var tmpMat42 = Matrix4x4.CreateFromAxisAngle(new Vector3(MathF.Cos(now), MathF.Sin(now), 0), 1f) * modelMatrix2;
        
        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView((2f * MathF.PI) / 5f, width / height, 1f, 100f);
        
        modelViewProjectionMatrix1 = tmpMat41 * viewMatrix * projectionMatrix;
        modelViewProjectionMatrix2 = tmpMat42 * viewMatrix * projectionMatrix;
    }
    
    // JS example:  ...
    public void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalSeconds;
        UpdateTransformationMatrix(frame.Width, frame.Height, time);
        
        using var pass = frame.BeginRenderPass(renderPassDescriptor);
        
        // Render(pass, config, vertexBuffer.In(), modelViewProjectionMatrix1);
        // Render(pass, config, vertexBuffer.In(), modelViewProjectionMatrix2);
    }
    
    [NoEmit]
	[VertexShader  ("shaders/basic.vert.wgsl",                  vert: "main")]
	[FragmentShader("shaders/vertexPositionColor.frag.wgsl",    frag: "main")]
    public static partial void Render(RenderPass pass, RenderConfig config,
        [Draw]  [VertexBuffer(0)]   InBuffer<float> verticesBuffer,
                [BindUniform(0, 0)] in Matrix4x4    modelViewProjectionMatrix);
    
    [NoEmit]
	[VertexShader  ("shaders/basic.vert.wgsl",                  vert: "main")]
	[FragmentShader("shaders/vertexPositionColor.frag.wgsl",    frag: "main")]
    public static partial void Shadow(RenderPass pass, RenderConfig config,
        [Draw]  [VertexBuffer(0)]   InBuffer<float> verticesBuffer,
                [BindUniform(0, 0)] in Matrix4x4    modelViewProjectionMatrix);
}
