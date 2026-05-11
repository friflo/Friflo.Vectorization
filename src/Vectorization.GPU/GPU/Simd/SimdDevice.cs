// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Friflo.Vectorization.GPU.Runtime;

internal sealed class SimdDevice : GpuDevice
{
    private         bool isDisposed;
    
    public override bool IsDisposed => isDisposed;
        
    internal SimdDevice(string label, int slotSize) : base(label, slotSize) { }

    public override void Dispose() {
        isDisposed = true;
    }

    public override GpuLimits GetDeviceLimits() {
        return new GpuLimits();
    }

    public override GpuBuffer<T> CreateBuffer<T>(int length, GpuBufferUsage usage, string bufferLabel)
    {
        var array = new T[length];
        return new SimdBuffer<T>(this, array, bufferLabel);
    }

    public override GpuBuffer<T> CreateBuffer<T>(T[] data, GpuBufferUsage usage, string bufferLabel) {
        return new SimdBuffer<T>(this, data, bufferLabel);
    }

    public override void Flush(bool wait = true) { }

    public override void Wait<T>(GpuBuffer<T> buffer) { }

    public override void SubmitGraph(GpuTask finalTask) { }
}