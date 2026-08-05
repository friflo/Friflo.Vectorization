// ReSharper disable RedundantUsingDirective
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

// ReSharper disable RedundantUsingDirective
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
namespace TestConsole;

public static partial class GenerateParams
{
	[Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTrianglesEmpty();
    
    
	[Shader("~/shaders/renderTest/deform.wgsl", compute: "cs_main")]
	[WorkgroupSize(64)]
    private static partial void DeformVertices();
    
    
    [Shader("~/shaders/shadowMapping/vertexShadow.wgsl",  vertex: "main")]
    private static partial void RenderShadowMap();
    
    
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static partial void RenderScene();
    
    
    [Shader("~/shaders/instancedCube/instanced.vert.wgsl",  vertex:   "main")] 
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",      fragment: "main")]
    private static partial void RenderInstancedCubes();
    
    
	[Shader("~/shaders/basic.vert.wgsl",								vertex:   "main")]
	[Shader("~/shaders/texturedCube/sampleTextureMixColor.frag.wgsl",	fragment: "main")]
    private static partial void RenderTexturedCube();
    
    
	[Shader("~/shaders/testTextureTypes.frag.wgsl",  fragment: "main")]
    public static partial void Tests_WGSL_Generate_textures();
    
    
    [Shader("~/shaders/tests/testStructs.wgsl")]
    private static partial void TestStructs();
}