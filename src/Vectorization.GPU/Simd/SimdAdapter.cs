// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Friflo.Vectorization.GPU;

public sealed class SimdAdapter : GpuAdapter
{
    private         bool isDisposed;
    
    public override bool IsDisposed => isDisposed;
    
    internal SimdAdapter() { }
    
    public override void Dispose() {
        isDisposed = true;
    }

    public override GpuDevice CreateDevice(string label, int maxTasks = 64, int slotSize = 65536)
    {
        return new SimdDevice(label, maxTasks);
    }

    public override GpuHandleDiff GenerateHandles() {
        return new GpuHandleDiff();
    }

    public override GpuAdapterInfo GetAdapterInfo() {
        return SimdAdapterInfo.Simd;
    }

    public override GpuLimits GetAdapterLimits() {
        return new GpuLimits();
    }
}

public sealed class SimdAdapterInfo : GpuAdapterInfo
{
    internal static readonly SimdAdapterInfo Scalar = new() {
        AdapterType         = GpuAdapterType.CPU,
        BackendType         = GpuBackendType.Scalar,
        Name                = "SIMD",
        DriverDescription   = "SIMD Driver"
    };
    
    internal static readonly SimdAdapterInfo Simd = new() {
        AdapterType         = GpuAdapterType.CPU,
        BackendType         = GpuBackendType.SIMD,
        Name                = "SIMD",
        DriverDescription   = "SIMD Driver"
    };
}