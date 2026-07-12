using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace TestConsole;

public static partial class GenerateParams
{
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTrianglesEmpty(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage]    InBuffer<TriangleStorage> mesh_data,
        [Map(1, 0)] [uniform]    in MyUniforms myUniforms);
    
    
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static partial void RenderCubeEmpty(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]               in Scene scene,
        [Map(0, 1)] [texture_depth_2d]      GpuTextureView shadowMap,
        [Map(0, 2)] [sampler_comparison]    GpuSampler shadowSampler,
        [Map(1, 0)] [uniform]               in Model model,
                    [VertexBuffer(0)]       InBuffer<float> position) // Opt: [IndexBuffer] InBuffer<ushort|uint> indices;
    
    
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    private static partial void RenderCube(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]               in Uniforms uniforms,
        [Map(0, 1)] [sampler]               GpuSampler mySampler,
        [Map(0, 2)] [texture_2d(ST.f32)]    GpuTextureView myTexture,
                    [VertexBuffer(0)]       InBuffer<float> position) // Opt: [IndexBuffer] InBuffer<ushort|uint> indices;
    
	[Shader("~/shaders/testTextureTypes.frag.wgsl",  fragment: "main")]
    public static partial void Tests_WGSL_Generate_textures(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [texture_1d(ST.f32)]                 GpuTextureView texture0,
        [Map(0, 1)] [texture_2d(ST.f32)]                 GpuTextureView texture1,
        [Map(0, 2)] [texture_2d_array(ST.i32)]           GpuTextureView texture2,
        [Map(0, 3)] [texture_3d(ST.i32)]                 GpuTextureView texture3,
        [Map(0, 4)] [texture_cube(ST.u32)]               GpuTextureView texture4,
        [Map(0, 5)] [texture_cube_array(ST.u32)]         GpuTextureView texture5,
        [Map(0, 6)] [texture_multisampled_2d(ST.i32)]    GpuTextureView texture6,
        [Map(0, 7)] [texture_depth_multisampled_2d]      GpuTextureView texture7,
        [Map(0,12)] [texture_depth_2d]                   GpuTextureView texture12,
        [Map(0,13)] [texture_depth_2d_array]             GpuTextureView texture13,
        [Map(0,14)] [texture_depth_cube]                 GpuTextureView texture14,
        [Map(0,15)] [texture_depth_cube_array]           GpuTextureView texture15,
        [Map(1, 0)] [sampler]                            GpuSampler sampler0,
        [Map(1, 1)] [sampler_comparison]                 GpuSampler sampler1);
}