// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

/// <summary>  Defines the execution strategy for compute operations. </summary>
/// <remarks>
/// The <see cref="ComputeMode"/> determines which backend (CPU or GPU)  is used to perform calculations.<br/>
/// When <see cref="Device"/> is selected, the <see cref="GpuDevice"/> chooses the most efficient 
/// supported execution strategy based on its specific hardware capabilities
/// (e.g., preferring GPU over SIMD, and SIMD over Scalar).
/// </remarks>
public enum ComputeMode {
    /// <summary> Automatically selects the optimal mode based on device capabilities. </summary>
    Device  = 0,
    /// <summary> Executes operations using scalar CPU instructions. </summary>
    Scalar  = 1,
    /// <summary> Executes operations using SIMD CPU instructions. </summary>
    SIMD    = 2,
    /// <summary> Executes operations using GPU compute shaders. </summary>
    GPU     = 3
}

public ref struct Buffer<T> where T : unmanaged
{
    public              Span<T>         span;
    public  readonly    GpuBuffer<T>    gpuBuffer;
    
    public 	            int 			Count => gpuBuffer?.Length ?? span.Length;
    
    public Buffer(Span<T> span) {
        this.span = span;
    }
    public Buffer(GpuBuffer<T> gpuBuffer) {
        this.gpuBuffer  = gpuBuffer;
        span            = gpuBuffer.Span;
    }
    
    public static implicit operator Buffer<T>(T[] array)                => new(array);
    public static implicit operator Buffer<T>(Span<T> span)             => new(span);
    public static implicit operator Buffer<T>(GpuBuffer<T> gpuBuffer)   => new(gpuBuffer);
}

