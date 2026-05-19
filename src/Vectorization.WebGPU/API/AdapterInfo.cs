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
    public      Adapter*    Adapter     { get; private init; }

    internal static WgpuAdapterInfo CreateAdapterInfo(AdapterInfo props, Adapter* adapter)
    {
        return new WgpuAdapterInfo {
            VendorID            = props.vendorID,
            DeviceID            = props.deviceID,
            AdapterType         = (GpuAdapterType)props.adapterType,
            BackendType         = (GpuBackendType)props.backendType,
            Name                = PtrToString(props.device),		// TODO was .Name
            DriverDescription   = PtrToString(props.description),	// TODO was .DriverDescription
            Adapter             = adapter
        };
    }
    
    private static string PtrToString(StringView stringView)
    {
        if (stringView.data == null) return string.Empty;
        return Marshal.PtrToStringAnsi((IntPtr)stringView.data, (int)stringView.length) ?? string.Empty;
    }
}
