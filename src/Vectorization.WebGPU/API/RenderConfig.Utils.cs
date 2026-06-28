// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

// -------------------------------------- ValueArray<T> --------------------------------------
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
    internal readonly T[] _array;

    public override string ToString() => $"{typeof(T).Name}[{Length}]";

    public ValueArray(ReadOnlySpan<T> span)
    {
        if (span.IsEmpty) return;
        _array = span.ToArray();
    }

    public int Length => _array?.Length ?? 0;
    
    /// <summary> Important: Returns an element by <c>ref readonly</c> to ensure the array remains immutable.</summary>
    public ref readonly T this[int index] {
        get {
            if (_array != null) return ref _array[index];
            throw new IndexOutOfRangeException();
        }
    }    

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

// -------------------------------------- ValueNullable<T> --------------------------------------
/// <summary>
/// An immutable, structural value-comparable wrapper around a nullable value.
/// </summary>
/// <remarks>
/// Ensures strict content-based equality checking (<see langword="Equals"/> and <see langword="GetHashCode"/>) 
/// inside <see langword="struct"/> configurations.
/// </remarks>
/// <typeparam name="T">The unmanaged value type of the underlying element.</typeparam>
public readonly struct ValueNullable<T> : IEquatable<ValueNullable<T>> where T : struct
{
    private readonly bool   _hasValue;
    private readonly T      _value;

    public bool HasValue => _hasValue;

    [UnscopedRef]
    public ref readonly T Value {
        get {
            if (_hasValue) {
                return ref _value;
            }
            throw new InvalidOperationException("Nullable object must have a value.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueNullable(in T value)
    {
        _value      = value;
        _hasValue   = true;
    }

    /// <summary>Retrieves the value of the current instance, or the default value of the underlying type.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault() => _value;

    /// <summary>Retrieves the value of the current instance, or the specified default value.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetValueOrDefault(in T defaultValue) => _hasValue ? _value : defaultValue;

    public override string ToString() => _hasValue ? _value.ToString() : string.Empty;

    // --- Structural Equality Implementation
    public bool Equals(ValueNullable<T> other)
    {
        if (_hasValue != other._hasValue) return false;
        if (!_hasValue) return true;
        return _value.Equals(other._value);
    }

    public override bool Equals(object obj) => obj is ValueNullable<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (!_hasValue) return 0;
        return _value.GetHashCode();
    }

    // --- Operator Overloads & Implicit Conversions (Syntax Sugar)
    
    /// <summary>Allows direct assignment from a pure value: <c>ValueNullable&lt;int&gt; val = 5;</c></summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueNullable<T>(in T value) => new(value);

    /// <summary>Seamlessly bridges the gap from native C# nullable syntax: <c>int? x = 5; ValueNullable&lt;int&gt; y = x;</c></summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ValueNullable<T>(in T? nativeNullable) => 
        nativeNullable.HasValue ? new ValueNullable<T>(nativeNullable.Value) : default;


    public static bool operator ==(in ValueNullable<T> left, in ValueNullable<T> right) =>  left.Equals(right);
    public static bool operator !=(in ValueNullable<T> left, in ValueNullable<T> right) => !left.Equals(right);
}



// -------------------------------------- NativeAllocator --------------------------------------
internal interface INativeSource<out TTarget>
    where TTarget : unmanaged
{
     internal TTarget GetNative(NativeAllocator allocator);
}


internal class NativeAllocator
{
    private readonly List<nint> pointers = [];
    
    internal unsafe TTarget* ArrayToNative<TFrom, TTarget>(ValueArray<TFrom> src)
        where TFrom   : struct, INativeSource<TTarget>
        where TTarget : unmanaged
    {
        var length = src.Length;
        if (length == 0) {
            return null;
        }
        var targets     = (TTarget*)Alloc(length, sizeof(TTarget));
        var srcArray    = src._array;

        for (int n = 0; n < length; n++) {
            targets[n] = srcArray[n].GetNative(this);
        }
        return targets; 
    }
    
    internal unsafe TTarget* NullableToNative<TFrom, TTarget>(in ValueNullable<TFrom> src, Func<TFrom, TTarget> converter)
        where TFrom   : struct
        where TTarget : unmanaged
    {
        if (!src.HasValue) {
            return null;
        }
        var targets = (TTarget*)Alloc(1, sizeof(TTarget));
        *targets    = converter(src.Value);
        return targets;
    }
    
    internal unsafe StringView StringToNative(string src)
    {
        if (string.IsNullOrEmpty(src)) {
            return default;
        }
        var len     = Encoding.UTF8.GetByteCount(src);
        var target  = (byte*)Alloc(len + 1, sizeof(byte));   // + 1   => be safe: add \0 terminator
        
        var dest = new Span<byte>(target, len);
        Encoding.UTF8.GetBytes(src, dest);
        target[len] = 0;
        
        return new StringView { data = (sbyte*)target, length = (uint)len };
    }
    
    private unsafe void* Alloc(int elementCount, int elementSize)
    {
        var target = (byte*)NativeMemory.Alloc((nuint)elementCount, (nuint)elementSize);
        pointers.Add((nint)target);
        return target;
    }
    
    internal unsafe void FreePointers()
    {
        foreach (var pointer in pointers) {
            NativeMemory.Free((void*)pointer);
        }
        pointers.Clear();
    }
}
