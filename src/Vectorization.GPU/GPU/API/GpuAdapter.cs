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
    public  uint    VendorID            { get; protected init; }
    public  uint    DeviceID            { get; protected init; }
    public  string  Name                { get; protected init; }
    public  string  DriverDescription   { get; protected init; }
}

public readonly struct GpuLimits
{
    public  ulong   MaxStorageBufferBindingSize         { get; init; }
    public  uint    MaxComputeWorkgroupStorageSize      { get; init; }
    public  uint    MaxBindGroups                       { get; init; }
    public  uint    MaxComputeInvocationsPerWorkgroup   { get; init; }
}
