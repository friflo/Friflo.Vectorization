// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public sealed unsafe class WgpuAdapterInfo : GpuAdapterInfo
{
    public      AdapterType AdapterType { get; private init; }
    public      BackendType BackendType { get; private init; }
    public      Adapter*    Adapter     { get; private init; }

    public override string ToString() {
        return $"GPU: {Name}  Backend: {BackendType}  Driver: {DriverDescription}  Type: {AdapterType}  Vendor: {VendorID:X}  Devive: {DeviceID:X}";
    }
    
    
    internal static WgpuAdapterInfo CreateAdapterInfo(AdapterInfo props, Adapter* adapter)
    {
        return new WgpuAdapterInfo {
            VendorID            = props.vendorID,
            DeviceID            = props.deviceID,
            AdapterType         = props.adapterType,
            BackendType         = props.backendType,
            Name                = PtrToString(props.Name),
            DriverDescription   = PtrToString(props.DriverDescription),
            Adapter             = adapter
        };
    }
    
    private static string PtrToString(byte* ptr)
    {
        if (ptr == null) return string.Empty;
        return Marshal.PtrToStringAnsi((IntPtr)ptr) ?? string.Empty;
    }
}
