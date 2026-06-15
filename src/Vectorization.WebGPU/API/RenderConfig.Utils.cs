// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Friflo.Vectorization.WebGPU.Runtime;

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


// -------------------------------------- NativeAllocator --------------------------------------
internal class NativeAllocator
{
    private readonly List<nint> pointers = [];
    
    internal unsafe TTarget* ArrayToNative<TFrom, TTarget>(ValueArray<TFrom> src, Func<TFrom, TTarget> converter)
        where TFrom   : struct
        where TTarget : unmanaged
    {
        var length = src.Length;
        if (length == 0) {
            return null;
        }
        var targets = (TTarget*)NativeMemory.Alloc((nuint)length, (nuint)sizeof(TTarget));
        pointers.Add((nint)targets);

        for (int n = 0; n < length; n++) {
            targets[n] = converter(src[n]);
        }
        return targets;
    }
    
    internal unsafe TTarget* NullableToNative<TFrom, TTarget>(TFrom? src, Func<TFrom, TTarget> converter)
        where TFrom   : struct
        where TTarget : unmanaged
    {
        if (!src.HasValue) {
            return null;
        }
        var targets = (TTarget*)NativeMemory.Alloc(1, (nuint)sizeof(TTarget));
        pointers.Add((nint)targets);

        *targets = converter(src.Value);
        return targets;
    }
    
    internal unsafe StringView StringToNative(string src)
    {
        if (string.IsNullOrEmpty(src)) {
            return default;
        }
        var len     = Encoding.UTF8.GetByteCount(src);
        var target  = (byte*)NativeMemory.Alloc((nuint)len + 1, sizeof(byte));   // + 1   => be safe: add \0 terminator
        pointers.Add((nint)target);
        
        var dest = new Span<byte>(target, len);
        Encoding.UTF8.GetBytes(src, dest);
        target[len] = 0;
        
        return new StringView { data = (sbyte*)target, length = (uint)len };
    }
    
    internal unsafe void FreePointers()
    {
        foreach (var pointer in pointers) {
            NativeMemory.Free((void*)pointer);
        }
        pointers.Clear();
    }
}
