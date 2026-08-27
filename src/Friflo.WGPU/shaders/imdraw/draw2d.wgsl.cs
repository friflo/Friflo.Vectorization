// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Numerics;
using Friflo.WGPU;

// ReSharper disable CheckNamespace
namespace Shaders.Imdraw;

[Source("~/shaders/imdraw/draw2d.wgsl")]
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct ImUniforms (in Matrix4x4 projection)
{
    [FieldOffset(  0)]  public  Matrix4x4 projection = projection;
}


