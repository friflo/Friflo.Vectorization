// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public sealed class GpuBuffer<T> : IDisposable where T : unmanaged
{
    private readonly    string          label;
    public  readonly    int             Length;
    public	readonly    long            Id;
    public	            GpuDevice       device { get; private set; }
    public              NativeTask      LastWritingTask;
    [EditorBrowsable(EditorBrowsableState.Never)]
    public	readonly    NativeBuffer<T> native;
        
    public              bool            IsDisposed => native.IsDisposed;
    public  override    string          ToString() => native.ToString();


    public void Dispose() {
        native.Dispose();
        device = null;
    }

    internal GpuBuffer(GpuDevice device, NativeBuffer<T> buffer, int length, string label, long id)
    {
        this.device = device;
        this.label  = label;
        Length      = length;
        Id          = id;
        native      = buffer;
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                device.Wait(this); // force Compute before CPU reads value
            }
            throw new NotImplementedException();
            // return InternalDownloadValue(index);
        }
    }

    public void WaitInDebug()
    {
        if (!device.DebugMode) {
            return;
        }
        device.Flush();
    }
    
    public void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        native.Download(this, targetArray);
    }
}

[Flags]
public enum GpuBufferUsage : int
{
    None            = 0x0,
    MapRead         = 0x1,
    MapWrite        = 0x2,
    CopySrc         = 0x4,
    CopyDst         = 0x8,
    Index           = 0x10,
    Vertex          = 0x20,
    Uniform         = 0x40,
    Storage         = 0x80,
    Indirect        = 0x100,
    QueryResolve    = 0x200,
//  Force32         = 0x7FFFFFFF,
}

internal static class GpuBufferUtils
{
    private static long IdCounter;
    
    internal static long NextId() => Interlocked.Increment(ref IdCounter);
}

