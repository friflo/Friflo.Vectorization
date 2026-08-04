// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ReplaceWithStringIsNullOrEmpty
namespace Friflo.WGSL.Transpiler.CodeFixes;

internal static class WgslTextureFormat
{
    internal static string MapWgslStorageFormatToEnumName(string? wgslFormat)
    {
        if (wgslFormat == null || wgslFormat == "") return "Undefined";

        return wgslFormat switch
        {
            // R (Red-only)
            "r32uint"             => "R32Uint",
            "r32sint"             => "R32Sint",
            "r32float"            => "R32Float",

            // RG (Red/Green)
            "rg32uint"            => "RG32Uint",
            "rg32sint"            => "RG32Sint",
            "rg32float"           => "RG32Float",

            // RGBA 8-Bit
            "rgba8unorm"          => "RGBA8Unorm",
            "rgba8snorm"          => "RGBA8Snorm",
            "rgba8uint"           => "RGBA8Uint",
            "rgba8sint"           => "RGBA8Sint",

            // RGBA 16-Bit
            "rgba16uint"          => "RGBA16Uint",
            "rgba16sint"          => "RGBA16Sint",
            "rgba16float"         => "RGBA16Float",

            // RGBA 32-Bit
            "rgba32uint"          => "RGBA32Uint",
            "rgba32sint"          => "RGBA32Sint",
            "rgba32float"         => "RGBA32Float",

            // most important extension (macOS/iOS Swapchains)
            "bgra8unorm"          => "BGRA8Unorm",

            _                     => wgslFormat
        };
    }
    
}