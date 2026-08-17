//HintName: VerifyShader/ShaderExample/InvalidWorkgroupSize.g.cs
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
    private static partial void InvalidWorkgroupSize() { }
}
