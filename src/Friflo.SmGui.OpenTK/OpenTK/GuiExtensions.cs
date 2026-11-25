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
    
    private const float Scale = 1 / 255f;
    public static Color4 AsOpenTkColor(this Color32 c) => new(c.R * Scale, c.G * Scale, c.B * Scale, c.A * Scale);
}