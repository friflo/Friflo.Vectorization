// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.GPU;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.CPU;

public sealed class CpuAdapter : GpuAdapter
{
    private             bool            isDisposed;
    private  readonly   GpuAdapterInfo  info;
    internal readonly   CpuInstance     instance;
    
    public override bool    IsDisposed => isDisposed;
    
    internal CpuAdapter(CpuInstance instance, GpuAdapterInfo info) {
        this.instance   = instance;
        this.info       = info;
    }
    
    public override void Dispose() {
        isDisposed = true;
    }

    public override GpuDevice CreateDevice(string label, int uniformBufferSize = 65536)
    {
        instance.deviceCount++;
        return new CpuDevice(this, label, uniformBufferSize);
    }

    public override GpuAdapterInfo GetAdapterInfo() {
        return info;
    }

    public override GpuLimits GetAdapterLimits() {
        return new GpuLimits();
    }
}

public sealed class CpuAdapterInfo : GpuAdapterInfo
{
    internal static readonly CpuAdapterInfo Scalar = new() {
        AdapterType         = GpuAdapterType.CPU,
        BackendType         = GpuBackendType.Scalar,
        Name                = "Scalar",
        DriverDescription   = "Scalar Driver"
    };
    
    internal static readonly CpuAdapterInfo Simd = new() {
        AdapterType         = GpuAdapterType.CPU,
        BackendType         = GpuBackendType.SIMD,
        Name                = "SIMD",
        DriverDescription   = "SIMD Driver"
    };
}