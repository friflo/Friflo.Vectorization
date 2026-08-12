// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct Vertex2D
{
    public  Vector2 position;   // 8
    public  Vector2 uv;         // 8
    public  uint    color;      // 4 (Rgba8Pack)
}
