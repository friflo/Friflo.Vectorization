// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.SmGui;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.SmGui;

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
}