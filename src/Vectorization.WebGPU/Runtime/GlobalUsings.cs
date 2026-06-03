// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

global using SegmentMap         = System.Collections.Generic.Dictionary<Friflo.Vectorization.WebGPU.Runtime.SegmentKey, Friflo.Vectorization.WebGPU.Runtime.SegmentState>;
global using BindGroupLayoutMap = System.Collections.Generic.Dictionary<ulong, Friflo.Vectorization.WebGPU.Runtime.WgpuBindGroupLayout>;
global using CommandListQueue	= System.Collections.Concurrent.ConcurrentQueue<Friflo.Vectorization.WebGPU.Runtime.CommandList>;
