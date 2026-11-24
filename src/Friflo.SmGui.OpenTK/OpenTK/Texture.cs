// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace

using System;

namespace Friflo.SmGui.OpenTK;

public sealed class OpenTkTextureData : IDisposable
{
    internal readonly int    TextureId;
    internal readonly int    Width;
    internal readonly int    Height;
    internal readonly string Label;
    internal readonly byte[] Pixels;

    public OpenTkTextureData(int textureId, int width, int height, string label, byte[] pixels)
    {
        TextureId = textureId;
        Width     = width;
        Height    = height;
        Label     = label;
        Pixels    = pixels;
    }

    public void Dispose() {

    }
}