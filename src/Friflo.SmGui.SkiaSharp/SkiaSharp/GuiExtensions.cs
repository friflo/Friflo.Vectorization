// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using SkiaSharp;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.SkiaSharp;

public static class SkiaSharpGuiExtensions
{
    public static SmTexture AsSmTexture(this SKBitmap bitmap)
    {
        return new SmTexture(bitmap, 0);
    }
    
    public static SKBitmap AsGpuTexture(in this SmTexture smTexture)
    {
        return (SKBitmap)smTexture.native;
    }
    
    public static SKColor AsSkColor (this Color32 c) => new(c.R, c.G, c.B, c.A);
}