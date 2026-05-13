// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Friflo.Vectorization.GPU;

public sealed class SimdAdapter : GpuAdapter
{
    private             bool            isDisposed;
    internal            long            deviceCount;
    internal            long            bufferCount;
    private readonly    GpuAdapterInfo  info;
    
    public override bool    IsDisposed => isDisposed;
    
    internal SimdAdapter(GpuAdapterInfo info) {
        this.info = info;
    }
    
    public override void Dispose() {
        isDisposed = true;
    }

    public override GpuDevice CreateDevice(string label, int maxTasks = 64, int slotSize = 65536)
    {
        deviceCount++;
        return new SimdDevice(this, label, maxTasks);
    }

    public override GpuHandleDiff GenerateHandles() {
        return new GpuHandleDiff
        {
            BackendType = info.BackendType,
            Devices     = new GpuHandle(deviceCount),
            Buffers     = new GpuHandle(bufferCount),
        };
    }

    public override GpuAdapterInfo GetAdapterInfo() {
        return info;
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