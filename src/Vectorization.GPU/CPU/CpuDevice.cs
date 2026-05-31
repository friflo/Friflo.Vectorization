// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable ConvertToAutoProperty
// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Friflo.Vectorization.CPU;

internal sealed class CpuDevice : GpuDevice
{
    private             bool                isDisposed;
    internal readonly   CpuAdapter          adapter;
    private  readonly   ComputeMode         defaultComputeMode;

    public   override   ComputeMode         DefaultComputeMode  => defaultComputeMode;
    public   override   bool                IsDisposed          => isDisposed;
        
    internal CpuDevice(CpuAdapter adapter, string label, int slotSize) : base(label, slotSize) {
        this.adapter = adapter;
        defaultComputeMode = adapter.GetAdapterInfo().BackendType == GpuBackendType.Scalar ? ComputeMode.Scalar : ComputeMode.SIMD;
    }

    public override void Dispose() {
        if (!isDisposed) adapter.deviceCount--;
        isDisposed = true;
        
        base.Dispose();  // calls GC.SuppressFinalize(this); to prevent execution of finalizer WHEN Dispose() is called manually
    }

    public override GpuLimits GetDeviceLimits() {
        return new GpuLimits();
    }

    public override GpuBuffer<T> CreateBuffer<T>(int length, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage)
    {
        adapter.bufferCount++;
        var array = new T[length];
        return new CpuBuffer<T>(this, array, bufferLabel);
    }

    public override GpuBuffer<T> CreateBuffer<T>(T[] data, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage) {
        adapter.bufferCount++;
        return new CpuBuffer<T>(this, data, bufferLabel);
    }

    public override void Flush(bool wait = true) { }

    protected internal override PipelineContext NewPipelineContext()
    {
        return new PipelineContext(this);
    }
}