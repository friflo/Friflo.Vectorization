using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_WGSL
{
    
    [Test]
    [Shader("~/shaders/triangle.wgsl")]
    public static void Tests_WGSL_Parse_triangle()
    {
        var (_, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var metadata = WgslSuperpowerParser.ParseShader(files[0].Content);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(2, metadata.Bindings.Count);
    }
    
    
    [Test]
    [Shader("~/shaders/raymarcher_no_texture.wgsl")]
    public static void Tests_WGSL_Parse_raymarcher_no_texture()
    {
        var (_, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var metadata = WgslSuperpowerParser.ParseShader(files[0].Content);
        
        Assert.AreEqual(4, metadata.Structs.Count);
        Assert.AreEqual(2, metadata.EntryPoints.Count);
        Assert.AreEqual(1, metadata.Bindings.Count);
    }
    
    
    [Test]
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateParameters()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var result = CodeFixer.CreateShaderParams(method, files);
        
        Assert.That(result.Errors.Length, Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [BindStorage(0, 0)]         InBuffer<TriangleStorage> mesh_data,
                    [BindUniform(1, 0)]         in MyUniforms myUniforms)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static void Tests_WGSL_GenerateSamplerTextureView()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var result = CodeFixer.CreateShaderParams(method, files);
        
        Assert.That(result.Errors.Length, Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [VertexBuffer(0)]           InBuffer<float> position, // Opt: [IndexBuffer] InBuffer<ushort|uint> indices,
                    [BindUniform(0, 0)]         in Scene scene,
                    [texture_depth_2d(0, 1)]    GpuTextureView shadowMap,
                    [SamplerComparison(0, 2)]    GpuSampler shadowSampler,
                    [BindUniform(1, 0)]         in Model model)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_Generate_texture_2d()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var result = CodeFixer.CreateShaderParams(method, files);
        
        
        Assert.That(result.Metadata.EntryPoints.Count,  Is.EqualTo(2));
        Assert.That(result.Errors.Length,               Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [VertexBuffer(0)]           InBuffer<float> position, // Opt: [IndexBuffer] InBuffer<ushort|uint> indices,
                    [BindUniform(0, 0)]         in Uniforms uniforms,
                    [SamplerFiltering(0, 1)]    GpuSampler mySampler,
                    [texture_2d(0, 2, ST.f32)]    GpuTextureView myTexture)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/testTextureTypes.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_Generate_textures()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var result = CodeFixer.CreateShaderParams(method, files);
        
        
        Assert.That(result.Metadata.EntryPoints.Count,  Is.EqualTo(1));
        Assert.That(result.Errors.Length,               Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo(
            """
            (RenderPass pass, RenderConfig config,
                    [texture_1d(0, 0, ST.f32)]    GpuTextureView texture0,
                    [texture_2d(0, 1, ST.f32)]    GpuTextureView texture1,
                    [texture_2d_array(0, 2, ST.i32)]    GpuTextureView texture2,
                    [texture_3d(0, 3, ST.i32)]    GpuTextureView texture3,
                    [texture_cube(0, 4, ST.u32)]    GpuTextureView texture4,
                    [texture_cube_array(0, 5, ST.u32)]    GpuTextureView texture5,
                    [texture_multisampled_2d(0, 6, ST.i32)]    GpuTextureView texture6,
                    [texture_depth_multisampled_2d(0, 7)]    GpuTextureView texture7,
                    [texture_depth_2d(0, 12)]    GpuTextureView texture12,
                    [texture_depth_2d_array(0, 13)]    GpuTextureView texture12,
                    [texture_depth_cube(0, 14)]    GpuTextureView texture12,
                    [texture_depth_cube_array(0, 15)]    GpuTextureView texture12,
                    [SamplerFiltering(1, 0)]    GpuSampler sampler0,
                    [SamplerComparison(1, 1)]    GpuSampler sampler1)
            """));
    }
}