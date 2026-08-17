using Friflo.WGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_WGSL
{
    
    [Test]
    [Shader("~/shaders/renderTest/triangle.wgsl")]
    public static void Tests_WGSL_Parse_triangle()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        
        Assert.AreEqual(4, module.Structs.Count);
        Assert.AreEqual(2, module.EntryPoints.Count);
        Assert.AreEqual(3, module.Bindings.Count);
    }
    
    
    [Test]
    [Shader("~/shaders/renderTest/raymarcher_no_texture.wgsl")]
    public static void Tests_WGSL_Parse_raymarcher_no_texture()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        
        Assert.AreEqual(4, module.Structs.Count);
        Assert.AreEqual(2, module.EntryPoints.Count);
        Assert.AreEqual(1, module.Bindings.Count);
    }
    
    
    [Test]
	[Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateParameters()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var mappings= TestWgslUtils.LoadTestMappings();
        var result  = CodeFixer.CreateShaderParams(module, mappings, false);
        
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
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var mappings= TestWgslUtils.LoadTestMappings();
        var result  = CodeFixer.CreateShaderParams(module, mappings, false);
        
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
	[Shader("~/shaders/basic.vert.wgsl",                                vertex:   "main")]
	[Shader("~/shaders/texturedCube/sampleTextureMixColor.frag.wgsl",   fragment: "main")]
    public static void Tests_WGSL_Generate_texture_2d()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var mappings= TestWgslUtils.LoadTestMappings();
        var result  = CodeFixer.CreateShaderParams(module, mappings, false);
        
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
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var mappings= TestWgslUtils.LoadTestMappings();
        var result  = CodeFixer.CreateShaderParams(module, mappings, false);
        
        Assert.That(module.EntryPoints.Count,   Is.EqualTo(1));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [texture_1d(ST.f32)]                                            GpuTextureView      texture0,
                    [Map(0, 1)] [texture_2d(ST.f32)]                                            GpuTextureView      texture1,
                    [Map(0, 2)] [texture_2d_array(ST.i32)]                                      GpuTextureView      texture2,
                    [Map(0, 3)] [texture_3d(ST.i32)]                                            GpuTextureView      texture3,
                    [Map(0, 4)] [texture_cube(ST.u32)]                                          GpuTextureView      texture4,
                    [Map(0, 5)] [texture_cube_array(ST.u32)]                                    GpuTextureView      texture5,
                    [Map(0, 6)] [texture_multisampled_2d(ST.i32)]                               GpuTextureView      texture6,
                    [Map(0, 7)] [texture_depth_multisampled_2d]                                 GpuTextureView      texture7,
                    [Map(0, 8)] [texture_storage_1d(TextureFormat.RGBA32Float, TSA.read)]       GpuTextureView      texture8,
                    [Map(0, 9)] [texture_storage_2d(TextureFormat.RGBA8Unorm, TSA.read)]        GpuTextureView      texture9,
                    [Map(0,10)] [texture_storage_2d_array(TextureFormat.RGBA8Uint, TSA.write)]  GpuTextureView      texture10,
                    [Map(0,11)] [texture_storage_3d(TextureFormat.R32Float, TSA.read_write)]    GpuTextureView      texture11,
                    [Map(0,12)] [texture_depth_2d]                                              GpuTextureView      texture12,
                    [Map(0,13)] [texture_depth_2d_array]                                        GpuTextureView      texture13,
                    [Map(0,14)] [texture_depth_cube]                                            GpuTextureView      texture14,
                    [Map(0,15)] [texture_depth_cube_array]                                      GpuTextureView      texture15,
                    [Map(1, 0)] [sampler]                                                       GpuSampler          sampler0,
                    [Map(1, 1)] [sampler_comparison]                                            GpuSampler          sampler1,
                    [Map(2, 0)] [storage]                                                       InBuffer<Vector4>   uniforms,
                    [Map(2, 1)] [uniform]                                                       in VertexData       vertices)
            """));
    }
    
    [Test]
    [Shader("~/shaders/instancedCube/instanced.vert.wgsl",  vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",      fragment: "main")]
    public static void Tests_WGSL_Generate_FixedSizeArrayUniform()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var mappings= TestWgslUtils.LoadTestMappings();
        var result  = CodeFixer.CreateShaderParams(module, mappings, false);
        
        Assert.That(module.EntryPoints.Count,    Is.EqualTo(2));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 0)] [uniform]           in Uniforms     uniforms,
                                [VertexBuffer(0)]   InBuffer<float> vertexBuffer)
            """));
    }
    
    [Test]
    [Shader("~/shaders/tests/testStructs.wgsl")]
    public static void Tests_WGSL_Generate_TestStructs()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var mappings= TestWgslUtils.LoadTestMappings();
        var result  = CodeFixer.CreateShaderParams(module, mappings, false);
        
        Assert.That(module.Structs.Count,       Is.EqualTo(13));
        Assert.That(module.Bindings.Count,      Is.EqualTo(13));
        Assert.That(module.EntryPoints.Count,   Is.EqualTo(0));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (RenderPass pass, RenderConfig config,
                    [Map(0, 1)] [uniform]   in TestStruct               uniform1,
                    [Map(0, 2)] [uniform]   in StructWithStructs        uniform2,
                    [Map(0, 3)] [uniform]   in Outer                    uniform3,
                    [Map(0, 4)] [uniform]   in FixeSizeArrayStruct1     uniform4,
                    [Map(0, 5)] [uniform]   in FixeSizeArrayStruct2     uniform5,
                    [Map(0, 6)] [uniform]   in VectorInUniform          uniform6,
                    [Map(0, 7)] [storage]   InBuffer<VectorInStorage>   storage7,
                    [Map(0, 8)] [uniform]   in TestParticle             uniform8,
                    [Map(0, 9)] [uniform]   in Vector4_UniArr_8         uniform9,
                    [Map(0,10)] [uniform]   in DirectUniform1           uniform10,
                    [Map(0,11)] [uniform]   in DirectUniform2_UniArr_8  uniform11,
                    [Map(0,12)] [storage]   InBuffer<DirectStorage>     storage12,
                    [Map(0,13)] [storage]   InBuffer<DirectStorage>     storage13)
            """));
        Assert.That(result.Comments, Is.EqualTo( // language=csharp
            """
                // [ ]  Add [Draw] to the vertex buffer parameter used to execute the draw call.
                #warning A uniform must not use dynamic sized buffers. See:  var<uniform> uniform10: array<DirectUniform1>
                // [ ]  If needed, add parameter: [IndexBuffer] InBuffer<ushort|uint> indices.
            
            """));
    }
    
    [Test]
    [Shader("~/shaders/renderTest/deform.wgsl", compute: "cs_main")]
    public static void Tests_WGSL_Generate_Compute()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_WGSL));
        var module  = CodeFixer.ParseWgslFiles(files);
        var mappings= TestWgslUtils.LoadTestMappings();
        var result  = CodeFixer.CreateShaderParams(module, mappings, true);
        
        Assert.That(module.Structs.Count,       Is.EqualTo(3));
        Assert.That(module.Bindings.Count,      Is.EqualTo(3));
        Assert.That(module.EntryPoints.Count,   Is.EqualTo(1));
        Assert.That(result.Parameters, Is.EqualTo( // language=csharp
            """
            (PipelineContext computeContext,
                    [Map(0, 0)] [storage]   InOutBuffer<VertexData> vertices,
                    [Map(0, 1)] [uniform]   in TestAddUniform       testAddUniform,
                    [Map(1, 0)] [uniform]   in TimeUniform          timeData)
            """));
        Assert.That(result.Comments, Is.EqualTo( // language=csharp
            """
                // [ ]  Add [Dispatch] to the parameter that defines the total item count for DispatchWorkgroups().
            
            """));
    }
}