// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public abstract class GpuBuffer<T> : IDisposable where T : unmanaged
{
    public  readonly    string      Label;
    public  readonly    int         Length;
    public	readonly    long        Id      = GpuBufferUtils.NextId();
    public	abstract    GpuDevice   Device  { get; }
    public              GpuTask     LastWritingTask;
    
    public  override    string      ToString() => $"{Label}({Id}): {(IsDisposed ? "Disposed" : "Alive")}";

    protected GpuBuffer(int length, string label)
    {
        Label   = label;
        Length  = length;
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                Device.Wait(this); // force Compute before CPU reads value
            }
            throw new NotImplementedException();
            // return InternalDownloadValue(index);
        }
    }

    public void WaitInDebug()
    {
        if (!Device.DebugMode) {
            return;
        }
        Device.Flush();
    }
    
    // --- abstract
    public  abstract    bool    IsDisposed { get; }
    public  abstract    void    Dispose();
    
    public  abstract    void    Download(GpuBuffer<T> gpuBuffer, T[] targetArray);
}

[Flags]
public enum GpuBufferUsage
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

