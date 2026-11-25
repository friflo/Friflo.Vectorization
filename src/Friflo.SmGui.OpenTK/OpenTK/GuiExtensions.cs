// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.



// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace

using OpenTK.Mathematics;

namespace Friflo.SmGui.OpenTK;

public static class OpenTkGuiExtensions
{
    public static SmTexture AsSmTexture(this OpenTkTextureData texture)
    {
        return new SmTexture(texture, texture.textureId);
    }

    public static OpenTkTextureData AsOpenTkTexture(in this SmTexture smTexture)
    {
        return (OpenTkTextureData)smTexture.native!;
    }
    
    public static Color4 AsOpenTkColor(this Color32 c) => new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
}