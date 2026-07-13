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
        var wgsl    = CodeFixer.CreateWgsl(method, files);
        var module  = WgslSuperpowerParser.ParseShader(wgsl);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(result.Errors.Length, Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [storage]   InBuffer<TriangleStorage>   mesh_data,
                    [Map(1, 0)] [uniform]   in MyUniforms               myUniforms)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static void Tests_WGSL_GenerateSamplerTextureView()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var wgsl    = CodeFixer.CreateWgsl(method, files);
        var module  = WgslSuperpowerParser.ParseShader(wgsl);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(result.Errors.Length, Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [uniform]               in Scene        scene,
                    [Map(0, 1)] [texture_depth_2d]      GpuTextureView  shadowMap,
                    [Map(0, 2)] [sampler_comparison]    GpuSampler      shadowSampler,
                    [Map(1, 0)] [uniform]               in Model        model,
                                [VertexBuffer(0)]       InBuffer<float> position /* Opt: [IndexBuffer] InBuffer<ushort|uint> indices */)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_Generate_texture_2d()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var wgsl    = CodeFixer.CreateWgsl(method, files);
        var module  = WgslSuperpowerParser.ParseShader(wgsl);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(module.EntryPoints.Count,    Is.EqualTo(2));
        Assert.That(result.Errors.Length,               Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [uniform]               in Uniforms     uniforms,
                    [Map(0, 1)] [sampler]               GpuSampler      mySampler,
                    [Map(0, 2)] [texture_2d(ST.f32)]    GpuTextureView  myTexture,
                                [VertexBuffer(0)]       InBuffer<float> position /* Opt: [IndexBuffer] InBuffer<ushort|uint> indices */)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/testTextureTypes.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_Generate_textures()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var wgsl    = CodeFixer.CreateWgsl(method, files);
        var module  = WgslSuperpowerParser.ParseShader(wgsl);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(module.EntryPoints.Count,    Is.EqualTo(1));
        Assert.That(result.Errors.Length,               Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [texture_1d(ST.f32)]                GpuTextureView  texture0,
                    [Map(0, 1)] [texture_2d(ST.f32)]                GpuTextureView  texture1,
                    [Map(0, 2)] [texture_2d_array(ST.i32)]          GpuTextureView  texture2,
                    [Map(0, 3)] [texture_3d(ST.i32)]                GpuTextureView  texture3,
                    [Map(0, 4)] [texture_cube(ST.u32)]              GpuTextureView  texture4,
                    [Map(0, 5)] [texture_cube_array(ST.u32)]        GpuTextureView  texture5,
                    [Map(0, 6)] [texture_multisampled_2d(ST.i32)]   GpuTextureView  texture6,
                    [Map(0, 7)] [texture_depth_multisampled_2d]     GpuTextureView  texture7,
                    [Map(0,12)] [texture_depth_2d]                  GpuTextureView  texture12,
                    [Map(0,13)] [texture_depth_2d_array]            GpuTextureView  texture13,
                    [Map(0,14)] [texture_depth_cube]                GpuTextureView  texture14,
                    [Map(0,15)] [texture_depth_cube_array]          GpuTextureView  texture15,
                    [Map(1, 0)] [sampler]                           GpuSampler      sampler0,
                    [Map(1, 1)] [sampler_comparison]                GpuSampler      sampler1)
            """));
    }
}