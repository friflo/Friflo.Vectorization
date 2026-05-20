// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.Vectorization.GPU;

// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Friflo.Vectorization.CPU;

internal sealed class CpuDevice : GpuDevice
{
    private             bool        isDisposed;
    internal readonly   CpuAdapter  adapter;
    private  readonly   ExeType     exeType;
    public   override   ExeType     ExeType     => exeType;
    public   override   bool        IsDisposed  => isDisposed;
        
    internal CpuDevice(CpuAdapter adapter, string label, int slotSize) : base(label, slotSize) {
        this.adapter = adapter;
        exeType = adapter.GetAdapterInfo().BackendType == GpuBackendType.Scalar ? ExeType.Scalar : ExeType.SIMD;
    }

    public override void Dispose() {
        if (!isDisposed) adapter.deviceCount--;
        isDisposed = true;
    }

    public override GpuLimits GetDeviceLimits() {
        return new GpuLimits();
    }

    public override GpuBuffer<T> CreateBuffer<T>(int length, GpuBufferUsage usage, string bufferLabel)
    {
        adapter.bufferCount++;
        var array = new T[length];
        return new CpuBuffer<T>(this, array, bufferLabel);
    }

    public override GpuBuffer<T> CreateBuffer<T>(T[] data, GpuBufferUsage usage, string bufferLabel) {
        adapter.bufferCount++;
        return new CpuBuffer<T>(this, data, bufferLabel);
    }

    public override void Flush(bool wait = true) { }

    public override void Wait<T>(GpuBuffer<T> buffer) { }

    public override void SubmitGraph(GpuTask finalTask) { }
}