//HintName: VerifyShader/ShaderExample/ExpectInOutBuffer.g.cs
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
    private static partial void ExpectInOutBuffer(
        PipelineContext             computeContext,
        InBuffer<VertexData>        vertices,
        TestAddUniform              testUniform,
        TimeUniform                 uniform) { }
}
