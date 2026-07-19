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
        var files   = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        
        Assert.AreEqual(4, module.Structs.Count);
        Assert.AreEqual(2, module.EntryPoints.Count);
        Assert.AreEqual(3, module.Bindings.Count);
    }
    
    
    [Test]
    [Shader("~/shaders/raymarcher_no_texture.wgsl")]
    public static void Tests_WGSL_Parse_raymarcher_no_texture()
    {
        var files   = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        
        Assert.AreEqual(4, module.Structs.Count);
        Assert.AreEqual(2, module.EntryPoints.Count);
        Assert.AreEqual(1, module.Bindings.Count);
    }
    
    
    [Test]
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateParameters()
    {
        var files   = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [storage]   InBuffer<VertexData>    mesh_data,
                    [Map(2, 0)] [uniform]   in MyUniforms           myUniforms,
                    [Map(2, 1)] [uniform]   in Vector2              model_offset)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static void Tests_WGSL_GenerateSamplerTextureView()
    {
        var files   = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [uniform]               in Scene        scene,
                    [Map(0, 1)] [texture_depth_2d]      GpuTextureView  shadowMap,
                    [Map(0, 2)] [sampler_comparison]    GpuSampler      shadowSampler,
                    [Map(1, 0)] [uniform]               in Model        model,
                                [VertexBuffer(0)]       InBuffer<float> vertexBuffer)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_Generate_texture_2d()
    {
        var files   = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(module.EntryPoints.Count,    Is.EqualTo(2));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [uniform]               in Uniforms     uniforms,
                    [Map(0, 1)] [sampler]               GpuSampler      mySampler,
                    [Map(0, 2)] [texture_2d(ST.f32)]    GpuTextureView  myTexture,
                                [VertexBuffer(0)]       InBuffer<float> vertexBuffer)
            """));
    }
    
    
    [Test]
	[Shader("~/shaders/tests/testTextureTypes.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_Generate_textures()
    {
        var files   = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(module.EntryPoints.Count,   Is.EqualTo(1));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [texture_1d(ST.f32)]                                            GpuTextureView          texture0,
                    [Map(0, 1)] [texture_2d(ST.f32)]                                            GpuTextureView          texture1,
                    [Map(0, 2)] [texture_2d_array(ST.i32)]                                      GpuTextureView          texture2,
                    [Map(0, 3)] [texture_3d(ST.i32)]                                            GpuTextureView          texture3,
                    [Map(0, 4)] [texture_cube(ST.u32)]                                          GpuTextureView          texture4,
                    [Map(0, 5)] [texture_cube_array(ST.u32)]                                    GpuTextureView          texture5,
                    [Map(0, 6)] [texture_multisampled_2d(ST.i32)]                               GpuTextureView          texture6,
                    [Map(0, 7)] [texture_depth_multisampled_2d]                                 GpuTextureView          texture7,
                    [Map(0, 8)] [texture_storage_1d(TextureFormat.RGBA32Float, TSA.read)]       GpuTextureView          texture8,
                    [Map(0, 9)] [texture_storage_2d(TextureFormat.RGBA8Unorm, TSA.read)]        GpuTextureView          texture9,
                    [Map(0,10)] [texture_storage_2d_array(TextureFormat.RGBA8Uint, TSA.write)]  GpuTextureView          texture10,
                    [Map(0,11)] [texture_storage_3d(TextureFormat.R32Float, TSA.read_write)]    GpuTextureView          texture11,
                    [Map(0,12)] [texture_depth_2d]                                              GpuTextureView          texture12,
                    [Map(0,13)] [texture_depth_2d_array]                                        GpuTextureView          texture13,
                    [Map(0,14)] [texture_depth_cube]                                            GpuTextureView          texture14,
                    [Map(0,15)] [texture_depth_cube_array]                                      GpuTextureView          texture15,
                    [Map(1, 0)] [sampler]                                                       GpuSampler              sampler0,
                    [Map(1, 1)] [sampler_comparison]                                            GpuSampler              sampler1,
                    [Map(2, 0)] [storage]                                                       InOutBuffer<Vector3>    uniforms,
                    [Map(2, 1)] [uniform]                                                       in Vector3              vertices)
            """));
    }
    
    [Test]
    [Shader("~/shaders/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    public static void Tests_WGSL_Generate_UniformMatrix4x4()
    {
        var files   = WgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var result  = CodeFixer.CreateShaderParams(module);
        
        Assert.That(module.EntryPoints.Count,    Is.EqualTo(2));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [uniform]           in Matrix4x4    uniforms,
                                [VertexBuffer(0)]   InBuffer<float> vertexBuffer)
            """));
    }
}