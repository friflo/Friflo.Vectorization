// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global

// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

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
