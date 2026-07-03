//HintName: VerifyShader/ShaderExample/RenderCube.g.cs
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    public static partial void RenderCube(
        RenderPass pass,
        RenderConfig config,
        InBuffer<Single> vertices,
        Uniforms uniforms,
        GpuSampler smoothFilter,
        GpuTextureView material)
    {
        // hello shader
    }
}