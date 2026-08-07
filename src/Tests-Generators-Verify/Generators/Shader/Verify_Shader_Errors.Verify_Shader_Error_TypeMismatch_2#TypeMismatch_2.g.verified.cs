//HintName: VerifyShader/ShaderExample/TypeMismatch_2.g.cs
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

namespace VerifyShader;

public partial class ShaderExample
{
    public static partial void TypeMismatch_2(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<Vector2>           triangles,
        in MyUniforms               myUniform,
        Vector2                     model_offset) { }
}
