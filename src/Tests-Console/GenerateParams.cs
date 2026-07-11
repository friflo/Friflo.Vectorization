using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace TestConsole;

public static partial class GenerateParams
{
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTrianglesEmpty();
    
    
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    public static partial void RenderCubeEmpty();
}