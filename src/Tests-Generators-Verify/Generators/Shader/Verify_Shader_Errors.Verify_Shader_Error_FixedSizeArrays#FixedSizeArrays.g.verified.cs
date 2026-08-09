//HintName: VerifyShader/ShaderExample/FixedSizeArrays.g.cs
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
    public static partial void FixedSizeArrays(
        RenderPass                  pass,
        RenderConfig                config,
        in int                      uniform0,
        in float                    uniform1) { }
}
