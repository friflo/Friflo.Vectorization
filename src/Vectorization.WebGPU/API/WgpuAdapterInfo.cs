// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public sealed unsafe class WgpuAdapterInfo
{
    public      uint        VendorID            { get; }
    public      uint        DeviceID            { get; }
    public      string      Name                { get; }
    public      string      DriverDescription   { get; }
    public      AdapterType AdapterType         { get; }
    public      BackendType BackendType         { get; }
    internal    Adapter*    Adapter             { get; }

    internal WgpuAdapterInfo(AdapterProperties props, Adapter* adapter)
    {
        VendorID            = props.VendorID;
        DeviceID            = props.DeviceID;
        AdapterType         = props.AdapterType;
        BackendType         = props.BackendType;
        Name                = PtrToString(props.Name);
        DriverDescription   = PtrToString(props.DriverDescription);
        Adapter             = adapter;
    }

    internal static string PtrToString(byte* ptr)
    {
        if (ptr == null) return string.Empty;
        return Marshal.PtrToStringAnsi((IntPtr)ptr) ?? string.Empty;
    }

    public override string ToString() {
        return $"GPU: {Name}  Backend: {BackendType}  Driver: {DriverDescription}  Type: {AdapterType}  Vendor: {VendorID:X}  Devive: {DeviceID:X}";
    }
}
