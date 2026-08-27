// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.ImGui.Headless;

public sealed class HeadlessTexture
{
    public              string  name;
    public              int     width;
    public              int     height;
    public  readonly    byte[]  rgbaPixels;

    public  override    string  ToString() => name;

    internal HeadlessTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        this.name       = name;
        this.width      = width;
        this.height     = height;
        this.rgbaPixels = rgbaPixels.ToArray();
    }
}
