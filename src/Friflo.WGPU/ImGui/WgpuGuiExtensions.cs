// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.ImGui;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.ImGui;

public static class WgpuGuiExtensions
{
    public static ImTexture ToImTexture(this GpuTextureView view)
    {
        return new ImTexture(view.texture, view.Handle);
    }
}