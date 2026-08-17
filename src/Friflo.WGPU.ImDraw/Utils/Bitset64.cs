// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public struct Bitset64<T> where T : struct, Enum
{
    private ulong value;

    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BitOperations.PopCount(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T id)
    {
        value |= 1UL << ToInt(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(T id)
    {
        value &= ~(1UL << ToInt(id));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Contains(T id)
    {
        return (value & (1UL << ToInt(id))) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Bitset64Enumerator<T> GetEnumerator() => new(value);

    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ToInt(T item)
    {
        int result = 0;
        Unsafe.As<int, T>(ref result) = item;
        return result;
    }
}

public struct Bitset64Enumerator<T> where T : struct, Enum
{
    private ulong remaining;

    public T Current
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        private set;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal Bitset64Enumerator(ulong value)
    {
        remaining = value;
        Current = default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MoveNext()
    {
        if (remaining == 0)
            return false;

        int index = BitOperations.TrailingZeroCount(remaining);

        int temp = index;
        Current = Unsafe.As<int, T>(ref temp);

        remaining &= remaining - 1;

        return true;
    }
}