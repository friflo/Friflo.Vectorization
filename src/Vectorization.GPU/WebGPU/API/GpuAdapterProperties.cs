// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuAdapterProperties
{
    public      uint        VendorID            { get; }
    public      uint        DeviceID            { get; }
    public      string      Name                { get; }
    public      string      DriverDescription   { get; }
    public      AdapterType AdapterType         { get; }
    public      BackendType BackendType         { get; }
    internal    Adapter*    Adapter             { get; }

    internal GpuAdapterProperties(AdapterProperties props, Adapter* adapter)
    {
        VendorID    = props.VendorID;
        DeviceID    = props.DeviceID;
        AdapterType = props.AdapterType;
        BackendType = props.BackendType;
        Name        = PtrToString(props.Name);
        Adapter     = adapter;
        DriverDescription = PtrToString(props.DriverDescription);
    }

    private static string PtrToString(byte* ptr)
    {
        if (ptr == null) return string.Empty;
        return Marshal.PtrToStringAnsi((IntPtr)ptr) ?? string.Empty;
    }

    public override string ToString() {
        return $"GPU: {Name}  Backend: {BackendType}  Driver: {DriverDescription}  Type: {AdapterType}  Vendor: {VendorID:X}  Devive: {DeviceID:X}";
    }
}

/*
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public unsafe struct GpuInstanceExtras
{
    public ChainedStruct Chain;
    public InstanceBackend Backends;
    public uint Flags;
    public Dx12Compiler Dx12ShaderCompiler;
    public Gles3MinorVersion Gles3MinorVersion;
    public byte* DxilPath;
    public byte* DxcPath;
}
*/