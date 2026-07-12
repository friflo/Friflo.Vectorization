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
    private readonly    GpuSampler          sampler;
    
    private             GpuTexture?         depthTexture;
    
    public void OnShutdown()
    {
        depthTexture?.Dispose();
        sampler.Dispose();
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
            size    = [shadowDepthTextureSize, shadowDepthTextureSize, 1],
            usage   = TextureUsage.RenderAttachment | TextureUsage.TextureBinding,
            format  = TextureFormat.Depth32Float
        });
        shadowDepthTextureView = shadowDepthTexture.CreateView();
        
        sampler = device.CreateSampler(new GpuSamplerDescriptor { compare = CompareFunction.Less });
        
        
        // Create some common descriptors used for both the shadow pipeline
        // and the color rendering pipeline.
        var vertexBuffers = new[] {
            new GpuVertexBufferLayout {
                arrayStride = Marshal.SizeOf<Vector3>() * 2,
                attributes = [
                    new GpuVertexAttribute {
                        // position
                        shaderLocation  = 0,
                        offset          = 0,
                        format          = VertexFormat.Float32x3
                    },
                    new GpuVertexAttribute {
                        // normal
                        shaderLocation  = 1,
                        offset          = Marshal.SizeOf<Vector3>(),
                        format          = VertexFormat.Float32x3
                    }
                ]
        }};
        var primitive = new GpuPrimitiveState {
            topology    = PrimitiveTopology.TriangleList,
            cullMode    = CullMode.Back
        };
        
        var shadowDesc = new GpuRenderPipelineDescriptor();
        shadowDesc.VertexState.buffers = vertexBuffers;
        shadowDesc.DepthStencilState = new GpuDepthStencilState {
            depthWriteEnabled   = true,
            depthCompare        = CompareFunction.Less,
            format              = TextureFormat.Depth32Float
        };
        shadowDesc.PrimitiveState = primitive;
        shadowConfig = shadowDesc.CreateConfig("Shadow Config");
        
        var renderDesc = wgpu.Config.Descriptor;
        renderDesc.VertexState.buffers = vertexBuffers;
        renderDesc.FragmentState = new GpuFragmentState {
            constants = [new GpuConstantEntry { key = "shadowDepthTextureSize", value = shadowDepthTextureSize }]
        };
        renderDesc.DepthStencilState = new GpuDepthStencilState {
            depthWriteEnabled   = true,
            depthCompare        = CompareFunction.Less,
            format              = TextureFormat.Depth24PlusStencil8
        };
        renderDesc.PrimitiveState = primitive;
        renderConfig = renderDesc.CreateConfig("Render Config");
        
        
        shadowPassDescriptor = new GpuRenderPassDescriptor {
            colorAttachments = [],
            depthStencilAttachment = new GpuRenderPassDepthStencilAttachment {
                view            = shadowDepthTextureView,
                depthClearValue = 1.0f,
                depthLoadOp     = LoadOp.Clear,
                depthStoreOp    = StoreOp.Store
            },
        };
        
        var lightPosition = new Vector3(50f, 100f, -100f);
        var lightViewMatrix = Matrix4x4.CreateLookAt(lightPosition, Vector3.Zero, Vector3.UnitY);

        // orthographic projection (Left, Right, Bottom, Top, Near, Far)
        var lightProjMatrix = Matrix4x4.CreateOrthographicOffCenter(-80f, 80f, -80f, 80f, -200f, 300f);

        var lightViewProjMatrix     = lightViewMatrix * lightProjMatrix;
        scene.lightViewProjMatrix   = lightViewProjMatrix;
        scene.cameraViewProjMatrix  = lightViewProjMatrix;
        scene.lightPos              = lightPosition;
    }

    // --- non-disposable fields
    private   readonly  Wgpu                    wgpu;
    private   readonly  RenderConfig            shadowConfig;
    private   readonly  RenderConfig            renderConfig;
    private   readonly  GpuTextureView          shadowDepthTextureView;              
    private   readonly  PerfLog                 perfLog             = new();
    private             Scene                   scene;
    private   readonly  Model                   model = new() { modelMatrix = Matrix4x4.CreateTranslation(new Vector3(0, -45, 0)) };
    
    private   readonly  Stopwatch               stopwatch           = Stopwatch.StartNew();
    private   readonly  GpuRenderPassDescriptor shadowPassDescriptor;
    private             GpuRenderPassDescriptor renderPassDescriptor= new() { colorAttachments = [ default ] };

    
    public void OnWindowChanged(int width, int height)
    {
        depthTexture?.Dispose();
        depthTexture = wgpu.Device.CreateTexture(new GpuTextureDescriptor {
            size    = [width, height],
            format  = TextureFormat.Depth24PlusStencil8,
            usage   = TextureUsage.RenderAttachment
        });
        
        // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/shadowMapping/main.ts#L194
        renderPassDescriptor.colorAttachments[0] = new GpuRenderPassColorAttachment {
            view        = default,  // Assigned later for each frame
            clearValue  = [0.5, 0.5, 0.5, 1],
            loadOp      = LoadOp.Clear,
            storeOp     = StoreOp.Store
        };
        renderPassDescriptor.depthStencilAttachment = new GpuRenderPassDepthStencilAttachment {
            view                = depthTexture.CreateView(),
            depthClearValue     = 1,
            depthLoadOp         = LoadOp.Clear,
            depthStoreOp        = StoreOp.Store,
            stencilClearValue   = 0,
            stencilLoadOp       = LoadOp.Clear,
            stencilStoreOp      = StoreOp.Store
        };
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/shadowMapping/main.ts#L300
    private static Matrix4x4 GetCameraViewProjMatrix(float width, float height, float now)
    {
        var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(2f * MathF.PI / 5f, width / height, 1f, 2000f);

        var eyePosition = new Vector3(0, 50, -100);
        
        var rad         = MathF.PI * (now / 2000f); 
        var rotation    = Matrix4x4.CreateRotationY(rad);
        eyePosition     = Vector3.Transform(eyePosition, rotation);

        var viewMatrix = Matrix4x4.CreateLookAt(eyePosition, Vector3.Zero, Vector3.UnitY);

        return Matrix4x4.Multiply(viewMatrix, projectionMatrix);
    }
    
    // JS example:  https://github.com/webgpu/webgpu-samples/blob/main/sample/shadowMapping/main.ts#L324
    public void OnFrame(in RenderFrame frame)
    {
        perfLog.Trace(5000);
        renderPassDescriptor.colorAttachments[0].view = frame.View;
        var time = (float)stopwatch.Elapsed.TotalMilliseconds;
        var cameraViewProj = GetCameraViewProjMatrix(frame.Width, frame.Height, time);
        scene.cameraViewProjMatrix = cameraViewProj;

        using (var pass = frame.BeginRenderPass(shadowPassDescriptor)) {
            Shadow(pass, shadowConfig, scene, model, vertexBuffer.In(), indexBuffer.In());
        }
        using (var pass = frame.BeginRenderPass(renderPassDescriptor)) {
            Render(pass, renderConfig, scene, shadowDepthTextureView, sampler, model, vertexBuffer.In(), indexBuffer.In());
        }
    }
    
    [NoEmit]
    [Shader("~/shaders/shadowMapping/vertexShadow.wgsl",  vertex: "main")]
    private static partial void Shadow(RenderPass pass, RenderConfig config,
                [Map(0, 0)] [uniform]           in Scene            scene,
                [Map(1, 0)] [uniform]           in Model            model,
                            [VertexBuffer(0)]   InBuffer<Vector3>   verticesBuffer,
        [Draw]              [IndexBuffer]       InBuffer<ushort>    indexBuffer);
    
    [NoEmit]

	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    private static partial void Render(RenderPass pass, RenderConfig config,
                [Map(0, 0)] [uniform]               in Scene            scene,
                [Map(0, 1)] [texture_depth_2d]      GpuTextureView      textureView,
                [Map(0, 2)] [SamplerComparison]     GpuSampler          sampler,
                [Map(1, 0)] [uniform]               in Model            model,
                            [VertexBuffer(0)]       InBuffer<Vector3>   verticesBuffer,
        [Draw]              [IndexBuffer]           InBuffer<ushort>    indexBuffer);
    

    [StructLayout(LayoutKind.Sequential)]
    public struct Scene {
        public Matrix4x4   lightViewProjMatrix;
        public Matrix4x4   cameraViewProjMatrix;
        public Vector3     lightPos;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct Model {
        public Matrix4x4   modelMatrix;
    }
}
