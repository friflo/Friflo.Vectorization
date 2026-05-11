// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.ComponentModel;

// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class SimdBuffer<T> : GpuBuffer<T> where T : unmanaged
{
    private   readonly  T[]         array;
    private             SimdDevice  device;
    internal
    protected override  Span<T>     Span        => array.AsSpan();
    public    override  GpuDevice   Device      => device;
    public    override  bool        IsDisposed  => device == null;
 
    
    internal SimdBuffer(SimdDevice device, T[] array, string label)
        : base(array.Length, label)
    {
        this.array = array;
        this.device = device;
    }

    public override void Dispose() {
        device = null;
    }

    public override void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) { }
}
