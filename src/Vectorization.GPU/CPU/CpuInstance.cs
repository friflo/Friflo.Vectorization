// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.GPU;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.CPU;

public sealed class CpuInstance : GpuInstance
{
    private         bool    isDisposed;
    internal        long    deviceCount;
    internal        long    bufferCount;
    
    public override bool IsDisposed => isDisposed;
    
    private CpuInstance() { }
    
    public static CpuInstance CreateInstance() {
        return new CpuInstance();
    }
    
    public CpuAdapter CreateAdapter(GpuBackendType backendType) {
        var info = backendType switch {
            GpuBackendType.SIMD     => CpuAdapterInfo.Simd,
            GpuBackendType.Scalar   => CpuAdapterInfo.Scalar,
            _                       => throw new NotSupportedException($"backendType: {backendType} not supported by CpuInstance")
        };
        return new CpuAdapter(this, info);
    }
    
    public override void Dispose() {
        isDisposed = true;
    }

    public override GpuAdapterInfo[] GetAdapterInfos() {
        return SimdAdapterInfos;
    }
    
    public override GpuHandleDiff GenerateHandles() {
        return new GpuHandleDiff
        {
            Devices     = new GpuHandle(deviceCount),
            Buffers     = new GpuHandle(bufferCount),
        };
    }
    
    private static readonly GpuAdapterInfo[] SimdAdapterInfos = [
        CpuAdapterInfo.Scalar,
        CpuAdapterInfo.Simd
    ];
}