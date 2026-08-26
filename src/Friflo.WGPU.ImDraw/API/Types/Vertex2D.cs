// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using System.Runtime.InteropServices;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct Vertex2D
{
    public  Vector2 position;   // 8
    public  Vector2 uv;         // 8
    public  uint    color;      // 4 (Rgba8Pack)
    
    public Vertex2D(Vector2 position, Vector2 uv, Color32 color)
    {
        this.position = position;
        this.uv = uv;
        this.color = color;
    }
}

/// <summary>
/// [0] Top-Left   [1] Top-Right   [2] Bottom-Right   [3] Bottom-Left
/// </summary>
[System.Runtime.CompilerServices.InlineArray(4)]
public struct VertexQuad
{
    private Vertex2D _element0;
}
