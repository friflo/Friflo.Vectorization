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
}

public abstract class GpuAdapterInfo
{
    public      uint        VendorID            { get; init; }
    public      uint        DeviceID            { get; init; }
    public      string      Name                { get; init; }
    public      string      DriverDescription   { get; init; }
}