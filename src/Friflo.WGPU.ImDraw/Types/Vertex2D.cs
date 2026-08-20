// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct Vertex2D (Vector2 position, Vector2 uv, Color32 color)
{
    public  Vector2 position    = position;   // 8
    public  Vector2 uv          = uv;         // 8
    public  uint    color       = color;      // 4 (Rgba8Pack)
}
