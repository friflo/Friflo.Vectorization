//HintName: VerifyShader/ShaderExample/DrawTriangles.g.cs
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample { 

    public static partial void DrawTriangles(
        RenderPass pass,
        RenderConfig config,
        InBuffer<VertexData> triangles,
        MyUniform myUniform)
    {
        // hello shader
    }
}