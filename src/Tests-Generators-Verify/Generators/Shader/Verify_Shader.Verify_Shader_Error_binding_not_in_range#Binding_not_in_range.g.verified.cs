//HintName: VerifyShader/ShaderExample/Binding_not_in_range.g.cs
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
    private static partial void Binding_not_in_range(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<Matrix4x4>         mvpMatrices,
        InBuffer<Matrix4x4>         mvpMatrices2,
        InBuffer<float>             verticesBuffer) { }
}
