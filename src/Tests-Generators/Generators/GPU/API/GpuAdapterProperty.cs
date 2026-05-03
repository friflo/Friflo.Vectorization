using System;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

public unsafe class GpuAdapterProperty
{
    public      uint        VendorID            { get; }
    public      uint        DeviceID            { get; }
    public      string      Name                { get; }
    public      string      DriverDescription   { get; }
    public      AdapterType AdapterType         { get; }
    public      BackendType BackendType         { get; }
    internal    Adapter*    Adapter             { get; }

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