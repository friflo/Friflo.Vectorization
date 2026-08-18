// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.GPU;

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.CPU;

internal sealed class CpuBuffer<T> : GpuBuffer<T> where T : unmanaged
{
    private             CpuDevice   device;
    public    override  GpuDevice   Device      => device;
    public    override  bool        IsDisposed  => device == null;
 
    
    internal CpuBuffer(CpuDevice device, Memory<T> hostMemory, string label)
        : base(hostMemory, label, 0, 0)
    {
        this.device = device;
    }

    public override void Dispose() {
        if (device != null) device.adapter.instance.bufferCount--;
        device = null;
    }
}
