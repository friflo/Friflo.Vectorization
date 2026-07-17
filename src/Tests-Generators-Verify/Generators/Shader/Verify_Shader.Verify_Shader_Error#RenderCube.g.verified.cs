//HintName: VerifyShader/ShaderExample/RenderCube.g.cs
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
    protected static partial void RenderCube(
        RenderPass                  pass,
        RenderConfig                config,
        in Uniforms                 uniforms,
        GpuSampler                  smoothFilter,
        GpuTextureView              material,
        InBuffer<float>             vertices) { }
}
