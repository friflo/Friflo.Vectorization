// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.SilkWebGPU;

public sealed unsafe class WgpuAdapterInfo
{
    public      uint        VendorID            { get; init; }
    public      uint        DeviceID            { get; init; }
    public      string      Name                { get; init; }
    public      string      DriverDescription   { get; init; }
    public      AdapterType AdapterType         { get; init; }
    public      BackendType BackendType         { get; init; }
    public      Adapter*    Adapter             { get; init; }

    public override string ToString() {
        return $"GPU: {Name}  Backend: {BackendType}  Driver: {DriverDescription}  Type: {AdapterType}  Vendor: {VendorID:X}  Devive: {DeviceID:X}";
    }
}
