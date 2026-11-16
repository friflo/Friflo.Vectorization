// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Microsoft.Xna.Framework.Graphics;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.KNI;


public static class KniGuiExtensions
{
    public static SmTexture AsSmTexture(this Texture2D texture2D)
    {
        return new SmTexture(texture2D, 0);
    }
    
    public static Texture2D AsGpuTexture(in this SmTexture tmTexture)
    {
        return (Texture2D)tmTexture.native;
    }
}