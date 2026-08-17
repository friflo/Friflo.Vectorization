//HintName: VerifyShader/ShaderExample/FixedSizeArrays.g.cs
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
    public static partial void FixedSizeArrays(
        RenderPass                  pass,
        RenderConfig                config,
        in int                      uniform0,
        in Int_UniArr_8             uniform1,
        in UniformWithArray         uniform2) { }
}
