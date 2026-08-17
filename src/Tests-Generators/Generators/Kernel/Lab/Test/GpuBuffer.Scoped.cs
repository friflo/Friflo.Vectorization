// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.GPU;

// ReSharper disable NotAccessedField.Local
// ReSharper disable once CheckNamespace
namespace Kernel.Lab;


/// Discarded design idea.<br/>
/// Reasons: Too complex in use. API does not fit and solves now real-world use case anymore
public interface IScopedReadBuffer<T> : IDisposable where T : unmanaged
{
    public BufferReader<T>  GetReader();
}

/// Discarded design idea.<br/>
/// Reasons: Too complex in use. API does not fit and solves now real-world use case anymore
public interface IScopedWriteBuffer<T> : IDisposable where T : unmanaged
{
    public BufferWriter<T>  GetWriter();
}

/// Discarded design idea.<br/>
/// Reasons: Too complex in use. API does not fit and solves now real-world use case anymore
public interface IScopedGpuBuffer<T> : IScopedReadBuffer<T>, IScopedWriteBuffer<T> where T : unmanaged { }


public readonly ref struct BufferWriter<T>  where T : unmanaged
{
    private readonly    GpuBuffer<T>    buffer;
    public              Span<T>         Span { get; }

    internal BufferWriter(GpuBuffer<T> buffer, Span<T> span) { 
        this.buffer = buffer; 
        Span        = span; 
    }

    public void Dispose() {
        // _buffer.Upload(Span); TODO 
    }
}

public readonly ref struct BufferReader<T> where T : unmanaged
{
    private readonly    GpuBuffer<T>        buffer;
    public              ReadOnlySpan<T>     Span { get; }

    internal BufferReader(GpuBuffer<T> buffer, ReadOnlySpan<T> span) 
    { 
        this.buffer = buffer; 
        Span        = span; 
    }

    public void Dispose() {
        // add an optional sync point
        // validate read is finished
    }
}
