// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


/// <summary>
/// 32-bit RGBA color struct mapped directly for GPU vertex buffers (R8G8B8A8_UNORM).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public struct Color32 : IEquatable<Color32>
{
    // Sequential byte offsets in memory (R-G-B-A)
    [FieldOffset(0)] public byte R;
    [FieldOffset(1)] public byte G;
    [FieldOffset(2)] public byte B;
    [FieldOffset(3)] public byte A;

    // Packed 32-bit representation (Direct register/equality access)
    [FieldOffset(0)] public uint Packed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color32(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color32(uint rawPacked)
    {
        Unsafe.SkipInit(out this);
        Packed = rawPacked;
    }

    /// <summary> Creates Color32 from a 0xRRGGBBAA hex literal (e.g. 0xFF0000FF for Red). </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Color32 FromRgbaHex(uint rgbaHex)
    {
        return new Color32(
            (byte)(rgbaHex >> 24),
            (byte)(rgbaHex >> 16),
            (byte)(rgbaHex >> 8),
            (byte) rgbaHex
        );
    }
    
    /// <summary> Converts Color32 to a 0xRRGGBBAA hex uint. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly uint ToRgbaHex()
    {
        return ((uint)R << 24) | ((uint)G << 16) | ((uint)B << 8) | A;
    }

    /// <summary> Creates Color32 from a string like "#RRGGBB" or "#RRGGBBAA". </summary>
    public static Color32 FromHex(ReadOnlySpan<char> hex)
    {
        if (hex.StartsWith("#")) hex = hex[1..];

        byte r = byte.Parse(hex[..2],  NumberStyles.HexNumber);
        byte g = byte.Parse(hex[2..4], NumberStyles.HexNumber);
        byte b = byte.Parse(hex[4..6], NumberStyles.HexNumber);
        byte a = hex.Length >= 8 ? byte.Parse(hex[6..8], NumberStyles.HexNumber) : (byte)255;

        return new Color32(r, g, b, a);
    }

    /// <summary> Returns a copy of this color with a new Alpha value. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Color32 WithAlpha(byte alpha) => new(R, G, B, alpha);

    // Equality & Operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Color32 left, Color32 right) => left.Packed == right.Packed;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Color32 left, Color32 right) => left.Packed != right.Packed;

    public readonly             bool    Equals(Color32 other)   => Packed == other.Packed;
    public readonly override    bool    Equals(object? obj)     => obj is Color32 other && Equals(other);
    public readonly override    int     GetHashCode()           => Packed.GetHashCode();

    /// <summary> Converts Color32 back to a 0xRRGGBBAA hex literal. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(Color32 color) => color.ToRgbaHex();
    
    /// <summary> Implicitly converts a 0xRRGGBBAA hex literal (e.g. 0xFF0000FF) to Color32. </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Color32(uint rgbaHex) => FromRgbaHex(rgbaHex);

    public readonly override string ToString() => $"RGBA({R}, {G}, {B}, {A})";


    // Presets
    // --- Special ---
    public static Color32 Transparent      => new(0, 0, 0, 0);
    
    // --- Basic Grayscale ---
    public static Color32 Black            => new(  0,   0,   0);
    public static Color32 DarkGray         => new( 64,  64,  64);
    public static Color32 Gray             => new(128, 128, 128);
    public static Color32 LightGray        => new(192, 192, 192);
    public static Color32 White            => new(255, 255, 255);
    
    // --- Primaries & Secondaries ---
    public static Color32 Red              => new(255,   0,   0);
    public static Color32 Green            => new(  0, 255,   0);
    public static Color32 Blue             => new(  0,   0, 255);
    public static Color32 Yellow           => new(255, 255,   0);
    public static Color32 Cyan             => new(  0, 255, 255);
    public static Color32 Magenta          => new(255,   0, 255);
    
    // --- Game Dev & UI Classics ---
    public static Color32 Orange           => new(255, 165,   0);
    public static Color32 Lime             => new( 50, 205,  50);
    public static Color32 CornflowerBlue   => new(100, 149, 237); // XNA/MonoGame
    public static Color32 Purple           => new(128,   0, 128);
    public static Color32 Pink             => new(255, 192, 203);
    public static Color32 Gold             => new(255, 215,   0);
    public static Color32 Teal             => new(  0, 128, 128);
    public static Color32 Navy             => new(  0,   0, 128);
    public static Color32 Crimson          => new(220,  20,  60);
}
