// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.TmGui.TUI;

// ReSharper disable NotAccessedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Headless;

internal sealed class CpuTexture
{
    internal readonly   string      name;
    internal readonly   int         width;
    internal readonly   int         height;
    internal readonly   byte[]      rgbaPixels;
    internal readonly   TuiSixel    sixel;

    public  override    string  ToString() => name;

    internal CpuTexture(string name, int width, int height, byte[] rgbaPixels)
    {
        this.name       = name;
        this.width      = width;
        this.height     = height;
        this.rgbaPixels = rgbaPixels;
        sixel           = new TuiSixel(width, height, this.rgbaPixels);
    }
}
