// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.CSharp;

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
public readonly struct ValueArray<T> : IEquatable<ValueArray<T>>, IEnumerable<T>  where T : IEquatable<T>
{
    internal readonly T[]? _array;

    public override string ToString() => $"{typeof(T).Name}[{Length}]";

    public ValueArray(ReadOnlySpan<T> span)
    {
        if (span.IsEmpty) return;
        _array = span.ToArray();
    }
    
    internal ValueArray(T[] array)
    {
        _array = array;
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
        
        return _array.AsSpan().SequenceEqual(other._array.AsSpan());
    }

    public override bool Equals(object? obj) => obj is ValueArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array == null) return 0;
        
        unchecked {
            int hash = (int)2166136261;
            foreach (var item in _array)
            {
                hash = (hash ^ (item?.GetHashCode() ?? 0)) * 16777619;
            }
            return hash;
        }
    }

    public static implicit operator ValueArray<T>(ReadOnlySpan<T> span) => new(span);
    
    public static implicit operator ValueArray<T>(T[] array) => new(array.AsSpan());

    // ---------- Enumerator ----------
    public ReadOnlySpan<T>.Enumerator GetEnumerator() => new ReadOnlySpan<T>(_array).GetEnumerator();

    // --- IEnumerable / IEnumerable<>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)(_array ?? [])).GetEnumerator();
    IEnumerator       IEnumerable.GetEnumerator() => ((IEnumerable<T>)(_array ?? [])).GetEnumerator();
}

internal static class ValueArrayExtensions
{
    internal static ValueArray<TSource> ToValueArray<TSource>(this IEnumerable<TSource> items) where TSource : IEquatable<TSource>
    {
        if (items is ValueArray<TSource>)
        {
            return (ValueArray<TSource>)items;
        }
        return new ValueArray<TSource>(items.ToArray());
    }
}


