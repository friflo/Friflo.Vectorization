// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public readonly struct RenderConfig
{
    public readonly int Id;
    
    internal RenderConfig(int id) {
        Id = id;
    }
    
    public RenderConfigDescriptor Descriptor => RenderConfigDescriptor.idToDescriptor[Id];
}

public record struct RenderConfigDescriptor
{
    public  WgpuColorTargetState    ColorTargetState    = new();
    public  WgpuPrimitiveState      PrimitiveState      = new();
    public  WgpuFragmentState       FragmentState       = new();
    public  WgpuMultisampleState    MultisampleState    = new();
    public  WgpuVertexState         VertexState         = new();
    
    public RenderConfigDescriptor() { }
    
    public RenderConfig GetConfig()
    {
        if (descriptorToId.TryGetValue(this, out var id)) {
            return new RenderConfig(id);
        }
        var descriptors = idToDescriptor;
        var config      = new RenderConfig(descriptors.Count);
        descriptors.Add(this);
        descriptorToId.Add(this, config.Id);
        return config;
    }
    
    internal static readonly    List<RenderConfigDescriptor>            idToDescriptor;
    internal static readonly    Dictionary<RenderConfigDescriptor, int> descriptorToId = [];
    
    static RenderConfigDescriptor()
    {
        var defaultDesc = new RenderConfigDescriptor();
        idToDescriptor = [defaultDesc];
        descriptorToId.Add(defaultDesc, 0);
    }
}


// ---------------------------------------- top level wgpu states ----------------------------------------
/// <summary> managed type for <see cref="ColorTargetState"/> </summary>
public record struct WgpuColorTargetState
{
    public  TextureFormat       format              = TextureFormat.BGRA8Unorm;
    public  BlendState?         blend;
    public  ulong               writeMask           = ColorWriteMask_All;
    
    public WgpuColorTargetState() { }
}

/// <summary> managed type for <see cref="PrimitiveState"/> </summary>
public record struct WgpuPrimitiveState
{
    public  PrimitiveTopology   topology            = PrimitiveTopology.TriangleList;
    public  IndexFormat         stripIndexFormat;
    public  FrontFace           frontFace;
    public  CullMode            cullMode;
    public  uint                unclippedDepth;
    
    public WgpuPrimitiveState() { }
    
    internal PrimitiveState GetNative() {
        return new PrimitiveState {
            topology            = topology,
            stripIndexFormat    = stripIndexFormat,
            frontFace           = frontFace,
            cullMode            = cullMode,
            unclippedDepth      = unclippedDepth
        };
    }
}

/// <summary> managed type for <see cref="FragmentState"/> </summary>
public record struct WgpuFragmentState
{
//  public  string                              entryPoint;     defined via [Shader] attribute
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuColorTargetState>    targets = [new() { format =  TextureFormat.BGRA8Unorm, writeMask = ColorWriteMask_All}];

    public WgpuFragmentState() { }

    internal ColorTargetState[] GetTargets() {
        return targets.ToArray(src => new ColorTargetState {
            format      = src.format,
            writeMask   = src.writeMask
        });
    }
    
    internal ConstantEntry[] GetConstants() {
        return constants.ToArray(src => new ConstantEntry {
            // key     = src.key,  // TODO
            value   = src.value
        });
    }
}

/// <summary> managed type for <see cref="MultisampleState"/> </summary>
public record struct WgpuMultisampleState
{
    public  uint    count                   = 1;            // 1 = normal rendering (no MSAA), >1  for Anti-Aliasing
    public  uint    mask                    = 0xFFFFFFFF;   // (Standard)
    public  bool    alphaToCoverageEnabled;
    
    public WgpuMultisampleState() { }
    
    internal MultisampleState GetNative() {
        return new MultisampleState {
            count                   = count,
            mask                    = mask,
            alphaToCoverageEnabled  = alphaToCoverageEnabled ? 1u : 0
        };
    }
}

/// <summary> managed type for <see cref="VertexState"/> </summary>
public record struct WgpuVertexState
{
//  public  ShaderModule*                       module;         defined via [Shader] attribute
//  public  StringView                          entryPoint;     defined via [Shader] attribute
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuVertexBufferLayout>  buffers;
    
    public WgpuVertexState() { }
    
    internal ConstantEntry[] GetConstants() {
        return constants.ToArray(src => new ConstantEntry {
            // key     = src.key,  // TODO
            value   = src.value
        });
    }
    
    internal VertexBufferLayout[] GetBuffers() {
        return buffers.ToArray(src => new VertexBufferLayout {
            arrayStride = src.arrayStride,
            stepMode    = src.stepMode
        });
    }
}

// ---------------------------------------- child level wgpu states ----------------------------------------
/// <summary> managed type for <see cref="ConstantEntry"/> </summary>
public record struct WgpuConstantEntry
{
    public  string  key;
    public  double  value;
}

/// <summary> managed type for <see cref="VertexBufferLayout"/> </summary>
public record struct WgpuVertexBufferLayout
{
    public  VertexStepMode              stepMode;
    public  ulong                       arrayStride;
    public  ValueArray<VertexAttribute> attributes;
    
    internal VertexAttribute[] GetAttributes() {
        return attributes.ToArray(src => new VertexAttribute {
            format          = src.format,
            offset          = src.offset,
            shaderLocation  = src.shaderLocation
        });
    }
}


// -------------------------------------------- ValueArray<> --------------------------------------------

/// <summary>
/// An immutable, structural value-comparable wrapper around a flat array.
/// </summary>
/// <remarks>
/// Enables content-based equality checking (<see langword="Equals"/> and <see langword="GetHashCode"/>) 
/// inside <see langword="record struct"/> contexts. Supports clean C# <c>[...]</c> collection syntax 
/// via implicit conversion.
/// </remarks>
/// <typeparam name="T">The unmanaged value type of the elements.</typeparam>
[CollectionBuilder(typeof(ValueArrayBuilder), nameof(ValueArrayBuilder.Create))]
public readonly struct ValueArray<T> : IEquatable<ValueArray<T>>, IEnumerable<T> where T : struct
{
    private readonly T[] _array;

    public ValueArray(ReadOnlySpan<T> span)
    {
        if (span.IsEmpty) return;
        _array = span.ToArray();
    }

    public int Length => _array?.Length ?? 0;
    
    public T this[int index] => _array != null ? _array[index] : throw new IndexOutOfRangeException();

    public bool Equals(ValueArray<T> other)
    {
        if (_array == other._array) return true;
        if (_array == null || other._array == null) return false;
        if (_array.Length != other._array.Length) return false;
        
        return ((IStructuralEquatable)_array).Equals(other._array, StructuralComparisons.StructuralEqualityComparer);
    }

    public override bool Equals(object obj) => obj is ValueArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array == null) return 0;
        return ((IStructuralEquatable)_array).GetHashCode(StructuralComparisons.StructuralEqualityComparer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueArray<T>(ReadOnlySpan<T> span) => new(span);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueArray<T>(T[] array) => new(array.AsSpan());

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_array ?? Array.Empty<T>())).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    
    
    internal TTarget[] ToArray<TTarget>(Func<T, TTarget> converter)
    {
        if (Length == 0) {
            return [];
        }
        var targets = new TTarget[Length];
        for (int n = 0; n < Length; n++) {
            targets[n] = converter(_array[n]);
        }
        return targets;
    }
}

/// <summary>
/// Internal compiler helper to enable the [...] collection expression for ValueArray.
/// </summary>
public static class ValueArrayBuilder
{
    public static ValueArray<T> Create<T>(ReadOnlySpan<T> items) where T : struct
    {
        return new ValueArray<T>(items);
    }
}