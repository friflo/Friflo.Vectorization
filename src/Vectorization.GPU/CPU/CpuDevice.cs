// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU;

// ReSharper disable InconsistentNaming
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
        
    internal CpuDevice(CpuAdapter adapter, string label, int uniformBufferSize) : base(label, uniformBufferSize) {
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

    public override GpuBuffer<T> CreateBuffer<T>(Memory<T> data, string bufferLabel, BufferProfile profile, BufferType type = BufferType.Storage) {
        adapter.bufferCount++;
        return new CpuBuffer<T>(this, data, bufferLabel);
    }

    protected internal override PipelineContext NewPipelineContext()
    {
        return new PipelineContext(this);
    }
}