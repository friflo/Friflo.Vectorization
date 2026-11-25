// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Raylib_cs;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Raylib;


public static class RaylibGuiExtensions
{
    public static SmTexture AsSmTexture(this RaylibTexture texture) {
        return new SmTexture(texture, 0);
    }

    public static RaylibTexture AsGpuTexture(in this SmTexture smTexture) {
        return (RaylibTexture)smTexture.native!;
    }
    
    public static Color AsRaylibColor(this Color32 color32) => new(color32.R, color32.G, color32.B, color32.A);
}