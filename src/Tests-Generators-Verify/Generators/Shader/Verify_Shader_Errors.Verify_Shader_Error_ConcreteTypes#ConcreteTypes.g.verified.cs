//HintName: VerifyShader/ShaderExample/ConcreteTypes.g.cs
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
    public static partial void ConcreteTypes(
        RenderPass                  pass,
        RenderConfig                config,
        in Point4                   uniform0,
        in Point3                   uniform1,
        in UniformPoint             uniform2) { }
}
