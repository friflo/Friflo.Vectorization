// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct Color32
{
    public readonly uint Value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Color32(byte r, byte g, byte b, byte a = 255)
    {
        Value = (uint)r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);
    }

    // Direct implicit cast to uint -> zero performance penalty at callsites
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator uint(Color32 color) => color.Value;

    // Presets
    // --- Special ---
    public static Color32 Transparent      => new(0, 0, 0, 0);
    
    // --- Basic Grayscale ---
    public static Color32 Black            => new(0, 0, 0);
    public static Color32 DarkGray         => new(64, 64, 64);
    public static Color32 Gray             => new(128, 128, 128);
    public static Color32 LightGray        => new(192, 192, 192);
    public static Color32 White            => new(255, 255, 255);
    
    // --- Primaries & Secondaries ---
    public static Color32 Red              => new(255, 0, 0);
    public static Color32 Green            => new(0, 255, 0);
    public static Color32 Blue             => new(0, 0, 255);
    public static Color32 Yellow           => new(255, 255, 0);
    public static Color32 Cyan             => new(0, 255, 255);
    public static Color32 Magenta          => new(255, 0, 255);
    
    // --- Game Dev & UI Classics ---
    public static Color32 Orange           => new(255, 165, 0);
    public static Color32 Lime             => new(50, 205, 50);
    public static Color32 CornflowerBlue   => new(100, 149, 237); // XNA/MonoGame
    public static Color32 Purple           => new(128, 0, 128);
    public static Color32 Pink             => new(255, 192, 203);
    public static Color32 Gold             => new(255, 215, 0);
    public static Color32 Teal             => new(0, 128, 128);
    public static Color32 Navy             => new(0, 0, 128);
    public static Color32 Crimson          => new(220, 20, 60);
}
