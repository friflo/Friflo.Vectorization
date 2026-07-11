using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace TestConsole;

public static partial class GenerateParams
{
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTrianglesEmpty();
    
    
    [Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    public static partial void RenderCubeEmpty();
}