//HintName: VerifyShader/ShaderExample/Binding_not_in_range.g.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.GPU;
using Friflo.GPU.Runtime;
using Friflo.WGPU;
using Friflo.WGPU.Runtime;

namespace VerifyShader;

public partial class ShaderExample
{
    public static partial void Binding_not_in_range(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<VertexData>        triangles,
        in MyUniforms               myUniform,
        Vector2                     model_offset) { }
}
