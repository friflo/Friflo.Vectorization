using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.CSharp;
using Friflo.WGSL.Transpiler.WGSL;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_GenerateTypes
{
    [Test]
    public static void Tests_WGSL_GenerateAllTypes()
    {
        var projectDir = TestWgslUtils.GetProjectDir();

        // var mappings = new  WgslType2CSharpType[] { new (CsTypeCode.vec2i, "CustomTypes", "Vector2i") };
        var mappings = TypeMappings.LoadTypeMappings($"{projectDir}/{TypeMappings.MappingPath}", out var errors);
        if (errors.Length > 0) {
            foreach (var error in errors) {
                Assert.Fail($"line: {error.line} - {error.message}");
            }
        }
        Assert.NotNull(mappings);
        Assert.That(mappings.Length, Is.EqualTo(5));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.vec2i,   "CustomTypes",         "Vector2i")));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.vec2u,   "CustomTypes",         "Vector2<uint>")));
        
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.mat2x2h, "OpenTK.Mathematics",  "Matrix2")));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.mat2x3h, "Silk.NET.Maths",      "Matrix2x3<Half>")));
        Assert.That(mappings, Has.Member(new TypeMapping(CsTypeCode.mat2x4h, "Unity.Mathematics",   "float2x4")));

        var files = WgslUtils.LoadAdditionalFilesRecursive($"{projectDir}/shaders");
        
        for (int n = 0; n < 1; n++) {
            var typeEmitter = new TypeGen();
            typeEmitter.EmitAllStructs(files, projectDir, mappings, errors);
        }
    }
    
    
    [Test]
	[Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateTypes_1()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var module  = CodeFixer.ParseWgslFiles(files);
        var types   = TypeGenerator.GenerateCSharpTypes(module);
        
        Assert.That(types.Types, Is.EqualTo( // language=csharp
            """
                [Source("~/shaders/renderTest/triangle.wgsl")]
                [StructLayout(LayoutKind.Sequential)]
                public struct VertexData {
                    public Vector4 position;
                    public Vector4 color;
                }
                
                [Source("~/shaders/renderTest/triangle.wgsl")]
                [StructLayout(LayoutKind.Sequential)]
                public struct MyUniforms {
                    public Vector4 tint_color;
                }
                
            
            """));
    }
    
    [Test]
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static void Tests_WGSL_GenerateTypes_2()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var module  = CodeFixer.ParseWgslFiles(files);
        var types   = TypeGenerator.GenerateCSharpTypes(module);
        
        Assert.That(types.Types, Is.EqualTo( // language=csharp
            """
                [Source("~/shaders/shadowMapping/vertex.wgsl")]
                [StructLayout(LayoutKind.Sequential)]
                public struct Scene {
                    public Matrix4x4 lightViewProjMatrix;
                    public Matrix4x4 cameraViewProjMatrix;
                    public Vector3 lightPos;
                }
                
                [Source("~/shaders/shadowMapping/vertex.wgsl")]
                [StructLayout(LayoutKind.Sequential)]
                public struct Model {
                    public Matrix4x4 modelMatrix;
                }
                
            
            """));
    }
    
    [Test]
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/texturedCube/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    public static void Tests_WGSL_GenerateTypes_3()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var module  = CodeFixer.ParseWgslFiles(files);
        var types   = TypeGenerator.GenerateCSharpTypes(module);
        
        Assert.That(types.Types, Is.EqualTo( // language=csharp
            """
                [Source("~/shaders/basic.vert.wgsl")]
                [StructLayout(LayoutKind.Sequential)]
                public struct Uniforms {
                    public Matrix4x4 modelViewProjectionMatrix;
                }
                
            
            """));
    }
    
    [Test]
	[Shader("~/shaders/instancedCube/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    public static void Tests_WGSL_GenerateTypes_4()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var module  = CodeFixer.ParseWgslFiles(files);
        var types   = TypeGenerator.GenerateCSharpTypes(module);
        
        Assert.That(types.Types, Is.EqualTo( // language=csharp
            """
                [Source("~/shaders/instancedCube/instanced.vert.wgsl")]
                [StructLayout(LayoutKind.Sequential)]
                public struct Uniforms {
                    public array modelViewProjectionMatrix;
                }
                
            
            """));
        Assert.That(types.Comments, Is.EqualTo( // language=csharp
            """
                // [ ]  Remove if you can reuse existing struct types
            
            """));
    }
}