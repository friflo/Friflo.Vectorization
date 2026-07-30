
// ReSharper disable InconsistentNaming
namespace CustomTypes;


/// <summary>
/// Maps to WGSL type: <c>vec2i</c><br/>
/// Mapped in <c>wgsl-types.ini</c>
/// </summary>
public struct Vector2i(int x, int y)
{
    public int x = x;
    public int y = y;

    public override string ToString() =>  $"({x}, {y})";
}

/// <summary>
/// Maps to WGSL type: <c>vec2u</c><br/>
/// Mapped in <c>wgsl-types.ini</c>
/// </summary>
public struct Vector2u(uint x, uint y)
{
    public uint x = x;
    public uint y = y;

    public override string ToString() =>  $"({x}, {y})";
}
