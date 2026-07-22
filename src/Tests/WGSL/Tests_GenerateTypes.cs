using System.Linq;
using System.Reflection;
using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using Friflo.WGSL.Transpiler.WGSL;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_GenerateTypes
{
    // [Test]
    public static void Tests_WGSL_GenerateAllTypes()
    {
        var projectDir = TestWgslUtils.GetProjectDir();
        var files = TestWgslUtils.LoadAdditionalFilesRecursive($"{projectDir}/shaders");

        var typeEmitter = new TypeEmitter();
        typeEmitter.EmitAllStructs(files, projectDir);
    }
    
    
    [Test]
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateTypes_1()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var module  = CodeFixer.ParseWgslFiles(files);
        var types   = TypeGenerator.GenerateCSharpTypes(module);
        
        Assert.That(types.Types, Is.EqualTo( // language=csharp
            """
                [Source("~/shaders/triangle.wgsl")]
                [StructLayout(LayoutKind.Sequential)]
                public struct VertexData {
                    public Vector4 position;
                    public Vector4 color;
                }
                
                [Source("~/shaders/triangle.wgsl")]
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
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
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
	[Shader("~/shaders/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    public static void Tests_WGSL_GenerateTypes_4()
    {
        var files   = TestWgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var module  = CodeFixer.ParseWgslFiles(files);
        var types   = TypeGenerator.GenerateCSharpTypes(module);
        
        Assert.That(types.Comments, Is.EqualTo( // language=csharp
            """
                // (i)  wgsl bindings do not use custom structs
            
            """));
    }
}