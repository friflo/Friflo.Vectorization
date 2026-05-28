// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter

using Friflo.Vectorization.GPU;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.CPU;

public sealed class CpuInstance : GpuInstance
{
    private         bool isDisposed;
    
    public override bool IsDisposed => isDisposed;
    
    public CpuAdapter CreateAdapter(GpuBackendType backendType) {
        var info = backendType switch {
            GpuBackendType.SIMD     => CpuAdapterInfo.Simd,
            GpuBackendType.Scalar   => CpuAdapterInfo.Scalar
        };
        return new CpuAdapter(info);
    }
    
    public override void Dispose() {
        isDisposed = true;
    }

    public override GpuAdapterInfo[] GetAdapterInfos() {
        return SimdAdapterInfos;
    }
    
    private static readonly GpuAdapterInfo[] SimdAdapterInfos = [
        CpuAdapterInfo.Scalar,
        CpuAdapterInfo.Simd
    ];
}