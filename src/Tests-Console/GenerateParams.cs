using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace TestConsole;

public static partial class GenerateParams
{
	[Shader("shaders/triangle.wgsl")]
    public static partial void DrawTrianglesEmpty();
    
    
    [VertexShader  ("shaders/basic.vert.wgsl",                  vert: "main")]
	[FragmentShader("shaders/vertexPositionColor.frag.wgsl",    frag: "main")]
    public static partial void RenderCubeEmpty();
}