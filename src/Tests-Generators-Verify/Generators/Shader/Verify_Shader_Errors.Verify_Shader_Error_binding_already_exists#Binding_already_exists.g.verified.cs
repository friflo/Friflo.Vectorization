//HintName: VerifyShader/ShaderExample/Binding_already_exists.g.cs
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
    public static partial void Binding_already_exists(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<VertexData>        triangles,
        InBuffer<VertexData>        triangles2,
        in MyUniforms               myUniform,
        Vector2                     model_offset) { }
}
