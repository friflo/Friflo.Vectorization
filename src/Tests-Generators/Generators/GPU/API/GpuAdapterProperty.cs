using System;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

namespace Friflo.Vectorization.GPU;

public unsafe class GpuAdapterProperty
{
    public      uint        VendorID            { get; init; }
    public      uint        DeviceID            { get; init; }
    public      string      Name                { get; init; }
    public      string      DriverDescription   { get; init; }
    public      AdapterType AdapterType         { get; init; }
    public      BackendType BackendType         { get; init; }
    internal    Adapter*    Adapter             { get; init; }

    internal GpuAdapterProperty(AdapterProperties props, Adapter* adapter)
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