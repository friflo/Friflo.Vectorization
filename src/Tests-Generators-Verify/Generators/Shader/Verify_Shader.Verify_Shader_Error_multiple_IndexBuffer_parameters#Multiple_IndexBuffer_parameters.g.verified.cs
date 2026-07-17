//HintName: VerifyShader/ShaderExample/Multiple_IndexBuffer_parameters.g.cs
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
    protected static partial void Multiple_IndexBuffer_parameters(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<ushort>            indexBuffer1,
        InBuffer<ushort>            indexBuffer2) { }
}
