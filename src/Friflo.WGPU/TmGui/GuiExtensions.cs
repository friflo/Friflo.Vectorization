// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.TmGui;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.TmGui;

public static class WgpuGuiExtensions
{
    public static TmTexture AsImTexture(this GpuTextureView textureView)
    {
        return new TmTexture(textureView.texture, textureView.Handle);
    }
    
    public static GpuTexture AsGpuTexture(in this TmTexture tmTexture)
    {
        return (GpuTexture)tmTexture.native;
    }
}