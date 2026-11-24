// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using OpenTK.Windowing.Common.Input;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
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

public static  class OpenTkUtils
{
    public static WindowIcon LoadWindowIcon(IGuiAssets assets)
    {
        var imageName = "Friflo.SmGui.OpenTK.Assets.opentk-blue-hexagon.png";
        using var stream = typeof(OpenTkTextureData).Assembly.GetManifestResourceStream(imageName)!;
        var imageAsset = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);
        var image = new Image(imageAsset.width, imageAsset.height, imageAsset.data);
        return new WindowIcon(image);
    }
}