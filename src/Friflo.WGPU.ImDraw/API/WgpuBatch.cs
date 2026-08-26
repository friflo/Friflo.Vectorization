// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.GPU;
using Friflo.WGPU;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.ImGui;

public class WgpuBatch : Batch2D
{
    internal WgpuBatch(ImGuiBackend backend, GpuDevice device, TextureFormat targetFormat, int maxVertices)
        : base(backend, device, targetFormat, maxVertices)
    {
    }
}