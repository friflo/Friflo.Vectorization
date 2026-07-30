// ReSharper disable CheckNamespace

using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace CustomTypes;

#pragma warning disable CS0169 // Field is never used

/// <summary> Maps to WGSL type vec2i - mapped in wgsl-types.ini</summary>
public struct Vector2i(int x, int y)
{
    public int x = x;
    public int y = y;

    public override string ToString() =>  $"({x}, {y})";
}

/// <summary> Maps to all vec2* types. NOT RECOMMENDED. Topic is already complex enough :) </summary>
public struct Vector2<T> where T : unmanaged
{
    public T x;
    public T y;
}
