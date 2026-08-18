// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Friflo.GPU;


// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
public sealed unsafe class WgpuAdapterInfo : GpuAdapterInfo
{
    internal static WgpuAdapterInfo CreateAdapterInfo(AdapterInfo props)
    {
        return new WgpuAdapterInfo {
            VendorID            = (int)props.vendorID,
            DeviceID            = (int)props.deviceID,
            AdapterType         = (GpuAdapterType)props.adapterType,
            BackendType         = (GpuBackendType)props.backendType,
            Name                = PtrToString(props.device),		// TODO was .Name
            DriverDescription   = PtrToString(props.description),	// TODO was .DriverDescription
        };
    }
    
    private static string PtrToString(StringView stringView)
    {
        if (stringView.data == null) return string.Empty;
        return Marshal.PtrToStringAnsi((IntPtr)stringView.data, (int)stringView.length) ?? string.Empty;
    }
}
