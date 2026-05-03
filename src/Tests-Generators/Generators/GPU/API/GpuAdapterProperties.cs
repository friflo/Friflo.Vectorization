using System;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU;

public class GpuAdapterProperties
{
    public uint         VendorID            { get; init; }
    public uint         DeviceID            { get; init; }
    public string       Name                { get; init; }
    public string       DriverDescription   { get; init; }
    public AdapterType  AdapterType         { get; init; }
    public BackendType  BackendType         { get; init; }

    public unsafe GpuAdapterProperties(AdapterProperties props)
    {
        VendorID    = props.VendorID;
        DeviceID    = props.DeviceID;
        AdapterType = props.AdapterType;
        BackendType = props.BackendType;
        Name        = PtrToString(props.Name);
        DriverDescription = PtrToString(props.DriverDescription);
    }

    private static unsafe string PtrToString(byte* ptr)
    {
        if (ptr == null) return string.Empty;
        
        // Marshal.PtrToStringAnsi liest bis zum ersten Null-Terminator \0
        return Marshal.PtrToStringAnsi((IntPtr)ptr) ?? string.Empty;
    }

    public override string ToString() {
        return $"GPU: {Name}  Backend: {BackendType}  Driver: {DriverDescription}  Type: {AdapterType}  Vendor: {VendorID:X}  Devive: {DeviceID:X}";
    }
}