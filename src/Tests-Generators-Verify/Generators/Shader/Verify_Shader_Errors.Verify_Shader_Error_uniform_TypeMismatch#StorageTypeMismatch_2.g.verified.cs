//HintName: VerifyShader/ShaderExample/StorageTypeMismatch_2.g.cs
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
    public static partial void StorageTypeMismatch_2(
        RenderPass                  pass,
        RenderConfig                config,
        InBuffer<VertexData>        triangles,
        int                         myUniform,
        Vector3                     model_offset) { }
}
