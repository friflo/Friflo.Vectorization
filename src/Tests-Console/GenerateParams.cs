// ReSharper disable RedundantUsingDirective
using System.Numerics;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
namespace TestConsole;

public static partial class GenerateParams
{
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTrianglesEmpty();
    
    
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static partial void RenderCubeEmpty();
    
    
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    private static partial void RenderCube();
    
    
	[Shader("~/shaders/testTextureTypes.frag.wgsl",  fragment: "main")]
    public static partial void Tests_WGSL_Generate_textures(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [texture_1d(ST.f32)]                                            GpuTextureView  texture0,
        [Map(0, 1)] [texture_2d(ST.f32)]                                            GpuTextureView  texture1,
        [Map(0, 2)] [texture_2d_array(ST.i32)]                                      GpuTextureView  texture2,
        [Map(0, 3)] [texture_3d(ST.i32)]                                            GpuTextureView  texture3,
        [Map(0, 4)] [texture_cube(ST.u32)]                                          GpuTextureView  texture4,
        [Map(0, 5)] [texture_cube_array(ST.u32)]                                    GpuTextureView  texture5,
        [Map(0, 6)] [texture_multisampled_2d(ST.i32)]                               GpuTextureView  texture6,
        [Map(0, 7)] [texture_depth_multisampled_2d]                                 GpuTextureView  texture7,
        [Map(0, 8)] [texture_storage_1d(TextureFormat.RGBA32Float, TSA.write)]      GpuTextureView  texture8,
        [Map(0, 9)] [texture_storage_2d(TextureFormat.RGBA8Unorm, TSA.write)]       GpuTextureView  texture9,
        [Map(0,10)] [texture_storage_2d_array(TextureFormat.RGBA8Uint, TSA.write)]  GpuTextureView  texture10,
        [Map(0,11)] [texture_storage_3d(TextureFormat.R32Float, TSA.write)]         GpuTextureView  texture11,
        [Map(0,12)] [texture_depth_2d]                                              GpuTextureView  texture12,
        [Map(0,13)] [texture_depth_2d_array]                                        GpuTextureView  texture13,
        [Map(0,14)] [texture_depth_cube]                                            GpuTextureView  texture14,
        [Map(0,15)] [texture_depth_cube_array]                                      GpuTextureView  texture15,
        [Map(1, 0)] [sampler]                                                       GpuSampler      sampler0,
        [Map(1, 1)] [sampler_comparison]                                            GpuSampler      sampler1);
    // Hint: If needed, add an optional parameter: [IndexBuffer] InBuffer<ushort|uint> indices. It cannot be inferred from wgsl.

    
    
	[Shader("~/shaders/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    public static partial void Tests_WGSL_GenerateTypes_4();

    
    [Shader("~/shaders/shadowMapping/vertexShadow.wgsl",  vertex: "main")]
    private static partial void Shadow();
}