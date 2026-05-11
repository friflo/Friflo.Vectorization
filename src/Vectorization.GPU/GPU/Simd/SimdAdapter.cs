// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.Vectorization.GPU.Runtime;

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
        return SimdAdapterInfo.Default;
    }

    public override GpuLimits GetAdapterLimits() {
        return new GpuLimits();
    }
}

public sealed class SimdAdapterInfo : GpuAdapterInfo
{
    internal static readonly SimdAdapterInfo Default = new() {
        Name                = "SIMD",
        DriverDescription   = "SIMD Driver"
    };
}