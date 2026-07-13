using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_GenerateTypes
{
    
    [Test]
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateTypes_1()
    {
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var wgsl    = CodeFixer.CreateWgsl(method, files);
        var types   = TypeGenerator.GenerateCSharpTypes(wgsl);
        
        Assert.That(types, Is.EqualTo( // language=csharp
            """
                public struct VertexData {
                    public Vector4 position;
                    public Vector4 color;
                }
                
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
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var wgsl    = CodeFixer.CreateWgsl(method, files);
        var types   = TypeGenerator.GenerateCSharpTypes(wgsl);
        
        Assert.That(types, Is.EqualTo( // language=csharp
            """
                public struct Scene {
                    public Matrix4x4 lightViewProjMatrix;
                    public Matrix4x4 cameraViewProjMatrix;
                    public Vector3 lightPos;
                }
                
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
        var (method, files) = WgslUtils.GetShaders(typeof(Tests_GenerateTypes));
        var wgsl    = CodeFixer.CreateWgsl(method, files);
        var types   = TypeGenerator.GenerateCSharpTypes(wgsl);
        
        Assert.That(types, Is.EqualTo( // language=csharp
            """
                public struct Uniforms {
                    public Matrix4x4 modelViewProjectionMatrix;
                }
                
            
            """));
    }
}