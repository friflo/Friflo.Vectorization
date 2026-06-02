// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.WebGPU.Runtime;

internal struct CommandList
{
    internal Queue<WgpuCommandBuffer> commandBuffers;
}