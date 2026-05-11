// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
namespace Friflo.Vectorization.GPU.Runtime;

public sealed class SimdInstance : GpuInstance
{
    private         bool isDisposed;
    
    public override bool IsDisposed => isDisposed;
    
    public SimdAdapter CreateAdapter() {
        return new SimdAdapter();
    }
    
    public override void Dispose() {
        isDisposed = true;
    }

    public override GpuAdapterInfo[] GetAdapterInfos() {
        return SimdAdapterInfos;
    }
    
    private static readonly GpuAdapterInfo[] SimdAdapterInfos = [SimdAdapterInfo.Default];
}