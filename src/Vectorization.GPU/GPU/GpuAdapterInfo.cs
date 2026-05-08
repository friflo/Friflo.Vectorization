// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Silk.NET.WebGPU;  // TODO remove

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuAdapterInfo
{
    public      uint        VendorID            { get; }
    public      uint        DeviceID            { get; }
    public      string      Name                { get; }
    public      string      DriverDescription   { get; }
    public      AdapterType AdapterType         { get; }
    public      BackendType BackendType         { get; }
    internal    IntPtr      Adapter             { get; }

    internal GpuAdapterInfo(AdapterProperties props, string name, string driver, IntPtr adapter)
    {
        VendorID    = props.VendorID;
        DeviceID    = props.DeviceID;
        AdapterType = props.AdapterType;
        BackendType = props.BackendType;
        Name        = name;;
        DriverDescription = driver;
        Adapter     = adapter;
    }

    public override string ToString() {
        throw new NotImplementedException();
    }
}
