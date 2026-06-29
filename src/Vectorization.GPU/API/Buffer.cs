// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU.Runtime;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

/// <summary> Specify a read-write buffer for a Kernel method as input/output parameter </summary>
/// <remarks>
/// <para>
///   Generic types that can be converted into an input/output parameter are:<br/>
///   <see cref="InOutView{T}"/>, <see cref="Span{T}"/> and <see cref="Memory{T}"/>.
/// </para>
/// <para>
///   When used as a shader method parameter the parameter must have a <c>[BindVertex]</c> or a <c>[VertexBuffer]</c> attribute.  
/// </para>
/// </remarks>
public readonly ref struct InOutBuffer<T> where T : unmanaged
{
    public  readonly    Span<T>         Span;
    public  readonly    GpuBuffer<T>    Buffer;
    public  readonly    int             Length;
    public  readonly    int             Offset;
    
    public             	nint            Handle 		=> Buffer.NativeHandle;
    
    public  override    string          ToString() 	=> BufferUtils.BufferToString(Buffer, "Span", Length);
    
    private InOutBuffer(Span<T> span) {
        Span    = span;
        Length  = span.Length;
    }
    
    private InOutBuffer(Memory<T> memory) {
        Span    = memory.Span;
        Length  = memory.Length;
    }
    
    private InOutBuffer(InOutView<T> view) {
        Buffer  = view.Buffer;
        Span    = view.Span;
        Offset  = view.Offset;
        Length  = view.Length;
    }
    
    // --- CPU buffers
    public static implicit operator InOutBuffer<T>(T[]           array)      => new(array);
    public static implicit operator InOutBuffer<T>(Span<T>       span)       => new(span);
    public static implicit operator InOutBuffer<T>(Memory<T>     memory)     => new(memory);
    // --- GPU buffers
    public static implicit operator InOutBuffer<T>(InOutView<T>  view)       => new(view);
    
    // public static implicit operator Buffer<T>(GpuBuffer<T>  gpuBuffer);      intentionally not available
}

/// <summary> Specify a read-only buffer for a Kernel method as input parameter </summary>
/// <remarks>
/// <para>
///   Generic types that can be converted into an input parameter are:<br/>
///   <see cref="InView{T}"/>, <see cref="InOutView{T}"/>, <see cref="ReadOnlySpan{T}"/> and <see cref="ReadOnlyMemory{T}"/>.
/// </para>
/// <para>
///   When used as a shader method parameter the parameter must have a <c>[BindVertex]</c> or a <c>[VertexBuffer]</c> attribute.  
/// </para>
/// </remarks>
public readonly ref struct InBuffer<T> where T : unmanaged
{
    public  readonly    ReadOnlySpan<T> Span;
    public  readonly    GpuBuffer<T>    Buffer;
    public  readonly    int             Length;
    public  readonly    int             Offset;
    
    public              nint            Handle 		=> Buffer.NativeHandle;

    public  override    string          ToString() 	=> BufferUtils.BufferToString(Buffer, "ReadOnlySpan", Length);

    private InBuffer(ReadOnlySpan<T> span) {
        Span        = span;
        Length      = span.Length;
    }
    
    private InBuffer(ReadOnlyMemory<T> memory) {
        Span        = memory.Span;
        Length      = memory.Length;
    }
    
    private InBuffer(InView<T> view) {
        Buffer  = view.Buffer;
        Span    = view.Span;
        Offset  = view.Offset;
        Length  = view.Length;
    }
    
    private InBuffer(InOutView<T> view) {
        Buffer  = view.Buffer;
        Span    = view.Span;
        Offset  = view.Offset;
        Length  = view.Length;
    }
    
    
    // --- CPU buffers
    public static implicit operator InBuffer<T>(ReadOnlySpan<T>   span)       => new(span);
    public static implicit operator InBuffer<T>(ReadOnlyMemory<T> memory)     => new(memory);
    // --- GPU buffers
    public static implicit operator InBuffer<T>(InView<T>         view)       => new(view);
    public static implicit operator InBuffer<T>(InOutView<T>      view)       => new(view); // read/write buffer also allowed
    
    // public static implicit operator InBuffer<T>      (GpuBuffer<T> gpuBuffer);   intentionally not available
    // public static implicit operator ReadOnlyBuffer<T>(T[] array));               intentionally not available
}



