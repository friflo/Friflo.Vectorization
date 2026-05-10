// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public sealed unsafe class WgpuAdapterInfo : GpuAdapterInfo
{
    public      AdapterType AdapterType         { get; init; }
    public      BackendType BackendType         { get; init; }
    public      Adapter*    Adapter             { get; init; }

    public override string ToString() {
        return $"GPU: {Name}  Backend: {BackendType}  Driver: {DriverDescription}  Type: {AdapterType}  Vendor: {VendorID:X}  Devive: {DeviceID:X}";
    }
}
