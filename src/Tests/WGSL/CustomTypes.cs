// ReSharper disable CheckNamespace

using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
namespace CustomTypes;

#pragma warning disable CS0169 // Field is never used

/// <summary>
/// Maps to <see cref="CsTypeCode.vec2i"/>
/// </summary>
public struct Vector2i
{
    public int x;
    public int y;
}