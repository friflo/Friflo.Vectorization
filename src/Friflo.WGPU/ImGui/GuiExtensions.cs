// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.ImGui2D;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.ImGui2D;

public static class WgpuGuiExtensions
{
    public static ImTexture AsImTexture(this GpuTextureView textureView)
    {
        return new ImTexture(textureView.texture, textureView.Handle);
    }
    
    public static GpuTextureView AsGpuTexture(in this ImTexture imTexture)
    {
        return new GpuTextureView((GpuTexture)imTexture.native, imTexture.handle);
    }
}