// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.WGPU;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WGPU;

public static class WgpuGuiExtensions
{
    public static SmTexture AsSmTexture(this GpuTextureView textureView)
    {
        return new SmTexture(textureView.texture, textureView.Handle);
    }
    
    public static GpuTexture AsGpuTexture(in this SmTexture tmTexture)
    {
        return (GpuTexture)tmTexture.native;
    }
    
    private const float Scale = 1 / 255f;
    public static GpuColor AsGpuColor(this Color32 c) =>  new GpuColor(c.R * Scale, c.G * Scale, c.B * Scale, c.A * Scale);
}