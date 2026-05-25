// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

public abstract class GpuAdapter : IDisposable
{
    public abstract bool            IsDisposed { get;  }
    
    public abstract void            Dispose();
    
    public abstract GpuDevice       CreateDevice (string label, int maxTasks = 64, int slotSize = 64 * 1024);
    public abstract GpuHandleDiff   GenerateHandles ();
    public abstract GpuAdapterInfo  GetAdapterInfo ();
    public abstract GpuLimits       GetAdapterLimits();
}

public abstract class GpuAdapterInfo
{
    public  GpuAdapterType  AdapterType         { get; protected init; }
    public  GpuBackendType  BackendType         { get; protected init; }
    public  int             VendorID            { get; protected init; }
    public  int             DeviceID            { get; protected init; }
    public  string          Name                { get; protected init; }
    public  string          DriverDescription   { get; protected init; }

    public  override string ToString()          => $"Backend: {BackendType}  Adapter: {AdapterType}";
}

public readonly struct GpuLimits
{
    public  long    MaxStorageBufferBindingSize         { get; init; }
    public  int     MaxComputeWorkgroupStorageSize      { get; init; }
    public  int     MaxBindGroups                       { get; init; }
    public  int     MaxComputeInvocationsPerWorkgroup   { get; init; }
}

public enum GpuAdapterType
{
    DiscreteGPU     = 1,
    IntegratedGPU   = 2,
    CPU             = 3,
    Unknown         = 4,
}

public enum GpuBackendType
{
    Undefined   = 0,
    Null        = 1,
    WebGPU      = 2,
    D3D11       = 3,
    D3D12       = 4,
    Metal       = 5,
    Vulkan      = 6,
    OpenGL      = 7,
    OpenGLES    = 8,
    // --- Friflo extensions
    Scalar      = 256,
    SIMD        = 257,
}
