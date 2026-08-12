// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct Vertex2D
{
    public  Vector2 Position;   // 8
    public  Vector2 UV;         // 8
    public  uint    Color;      // 4 (Rgba8Pack)
}
