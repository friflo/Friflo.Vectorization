// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class GpuBuffer : IDisposable
{
    public    readonly  string      Label;
    public    readonly  int         Length;
    public	  readonly  long        Id              = GpuBufferUtils.NextId();
    public	  abstract  GpuDevice   Device          { get; }
    public              GpuTask     LastWritingTask { get; protected set; }
    public    override  string      ToString()      => $"{Label}({Id}): {(IsDisposed ? "Disposed" : "Alive")}";
    
    // --- abstract
    public  abstract    bool        IsDisposed { get; }
    public  abstract    void        Dispose();
    
    protected GpuBuffer(int length, string label)
    {
        Label   = label;
        Length  = length;
    }
}

public interface IReadOnlyGpuBuffer<T> : IDisposable where T : unmanaged
{
    public ReadOnlyView<T>  ReadOnlyView { get; }
}

public abstract class GpuBuffer<T> : GpuBuffer, IReadOnlyGpuBuffer<T>  where T : unmanaged
{
    internal readonly  Memory<T>  hostMemory;
    
    /// <summary> Gets the raw CPU-side backing memory for this buffer. </summary>
    /// <remarks>
    /// <b>Synchronization Notice:</b> This memory is not automatically synchronized with the GPU.
    /// <list type="bullet">
    /// <item> <b>Concurrency:</b><br/>
    ///   CPU and GPU must not access this memory simultaneously to avoid data races.
    ///   Ensure the GPU has finished all pending work before modifying this memory.
    /// </item>
    /// <item> <b>Explicit Sync:</b><br/>
    ///   Modifications to this memory are only reflected on the GPU after calling <c>Upload()</c>.
    ///   GPU updates are only visible in this memory after calling <c>Download()</c>.
    /// </item>
    /// </list>
    /// </remarks>
    public Memory<T>  HostMemory => hostMemory;

    protected GpuBuffer(Memory<T> hostMemory, string label) :  base(hostMemory.Length, label) {
        this.hostMemory = hostMemory;
    }
    
    public BufferView<T>    Slice     (int start, int length) => new (this, start, length);
    public ReadOnlyView<T>  AsReadOnly(int start, int length) => new (this, start, length);
    
    public BufferView<T>    BufferView      => new (this, 0, Length);
    public ReadOnlyView<T>  ReadOnlyView    => new (this, 0, Length);

    
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

