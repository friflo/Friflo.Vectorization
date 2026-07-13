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
    public static partial void Tests_WGSL_Generate_textures();
    
    
	[Shader("~/shaders/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    public static partial void Tests_WGSL_GenerateTypes_4();

    
    [Shader("~/shaders/shadowMapping/vertexShadow.wgsl",  vertex: "main")]
    private static partial void Shadow();
}