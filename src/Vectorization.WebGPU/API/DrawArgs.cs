// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU;


// ReSharper disable once CheckNamespace
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable FieldCanBeMadeReadOnly.Global
namespace Friflo.Vectorization.WebGPU;

/// <summary>
/// When used as a <c>[Shader]</c> method parameter, it sets the parameters of<br/>
/// <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/draw"><c>draw()</c></a> or
/// <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPURenderPassEncoder/drawIndexed"><c>drawIndexed()</c></a>.
/// </summary>
/// <remarks>
/// This structure encapsulates geometry counts and index offsets.<br/>
/// It supports two primary execution patterns:
/// <list type="bullet">
///   <item>
///     <b>Single Parameter:</b> <br/>
///     Executes a single draw call using the provided configuration.
///   </item>
///   <item>
///     <b>Collection Parameter (<see cref="ReadOnlySpan{T}"/>, <see cref="Span{T}"/>, or <c>DrawArgs[]</c>):</b><br/>
///     Enables <b>CPU-driven Multi-Draw</b> (Batch-Rendering).<br/>
///     Executes a draw call for each element in the collection via an allocation-free loop.
///   </item>
/// </list>
/// </remarks>
public struct DrawArgs
{
    public int  count;
    public int  instanceCount;
    public int  first;
    public int  firstInstance;

    public DrawArgs()
    {
        instanceCount = 1;
    }

    public DrawArgs(int count = 0, int instanceCount = 1, int first = 0, int firstInstance = 0)
    {
        this.count          = count;
        this.instanceCount  = instanceCount;
        this.first          = first;
        this.firstInstance  = firstInstance;
    }
    
    public static DrawArgs InstanceCount<T>(in InBuffer<T> buffer) where T : unmanaged
    {
        return new DrawArgs { instanceCount = buffer.Length };
    }
}