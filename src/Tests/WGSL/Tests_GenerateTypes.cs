using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.CodeFixes;
using NUnit.Framework;


// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_GenerateTypes
{
    
    [Test]
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static void Tests_WGSL_GenerateTypes1()
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
                
                public struct TriangleStorage {
                    public VertexData[] triangles;
                }
                
                public struct MyUniforms {
                    public Vector4 tint_color;
                }
                
                public struct VertexOutput {
                    public Vector4 clip_position;
                    public Vector4 color;
                }
                
            
            """));
    }
}