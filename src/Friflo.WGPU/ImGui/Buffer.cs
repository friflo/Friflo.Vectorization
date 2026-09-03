// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.GPU;
using Friflo.TmGui;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGPU.TmGui;

internal sealed class TmWgpuBuffer<T> : TmBuffer<T> where T : unmanaged
{
    internal readonly GpuBuffer<T>  native;
    public   override Memory<T>     Memory => native.hostMemory;
    
    public TmWgpuBuffer(GpuBuffer<T> buffer) {
        native = buffer;
    }

    public override void Dispose() {
        native.Dispose();
    }
    
    public override void Write(int start, int length) {
        native.In(start, length).Write();
    }
}