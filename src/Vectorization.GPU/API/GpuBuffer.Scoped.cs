// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;


// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public interface IScopedReadBuffer<T> : IDisposable where T : unmanaged
{
    public BufferReader<T>  GetReader();
}

public interface IScopedWriteBuffer<T> : IDisposable where T : unmanaged
{
    public BufferWriter<T>  GetWriter();
}

public interface IScopedGpuBuffer<T> : IScopedReadBuffer<T>, IScopedWriteBuffer<T> where T : unmanaged { }


public readonly ref struct BufferWriter<T>  where T : unmanaged
{
    private readonly    GpuBuffer<T>    _buffer;
    public              Span<T>         Span { get; }

    internal BufferWriter(GpuBuffer<T> buffer, Span<T> span) { 
        _buffer = buffer; 
        Span = span; 
    }

    public void Dispose() {
        // _buffer.Upload(Span); TODO 
    }
}

public readonly ref struct BufferReader<T> where T : unmanaged
{
    private readonly    GpuBuffer<T>        _buffer;
    public              ReadOnlySpan<T>     Span { get; }

    internal BufferReader(GpuBuffer<T> buffer, ReadOnlySpan<T> span) 
    { 
        _buffer = buffer; 
        Span = span; 
    }

    public void Dispose() {
        // add an optional sync point
        // validate read is finished
    }
}
