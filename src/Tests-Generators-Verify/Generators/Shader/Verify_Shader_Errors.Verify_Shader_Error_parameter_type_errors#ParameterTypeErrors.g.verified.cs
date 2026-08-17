//HintName: VerifyShader/ShaderExample/ParameterTypeErrors.g.cs
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
    private static partial void ParameterTypeErrors(
        RenderPass                  pass,
        RenderConfig                config,
        Object                      mvpMatrices,
        Object                      vertices,
        InBuffer<Vector3>           verticesBuffer1,
        Object                      verticesBuffer2,
        Object                      indexBuffer1,
        InBuffer<float>             indexBuffer2,
        Object                      texture0,
        Object                      texture1,
        Object                      texture2,
        Object                      texture3,
        Object                      texture4,
        Object                      texture5,
        Object                      texture6,
        Object                      texture7,
        Object                      texture8,
        Object                      texture9,
        Object                      texture10,
        Object                      texture11,
        Object                      texture12,
        Object                      texture13,
        Object                      texture14,
        Object                      texture15,
        Object                      sampler0,
        Object                      sampler1) { }
}
