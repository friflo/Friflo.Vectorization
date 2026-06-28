// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;


// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.WebGPU.Runtime;

internal interface INativeSource<out TTarget>
    where TTarget : unmanaged
{
     internal TTarget GetNative(NativeAllocator allocator);
}


internal unsafe class NativeAllocator : IDisposable
{
    private readonly    byte*       nativeMemory;
    private             int         nativeMemoryPos;
    private const       int         NativeMemoryMax = 4096;
    private readonly    List<nint>  pointers = [];
    
    internal NativeAllocator() {
        nativeMemory = (byte*)NativeMemory.Alloc(NativeMemoryMax);
    }

    public void Dispose()
    {
        NativeMemory.Free(nativeMemory);
        Clear();
    }

    internal TTarget* ArrayToNative<TFrom, TTarget>(ValueArray<TFrom> src)
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
    
    internal TTarget* NullableToNative<TFrom, TTarget>(in ValueNullable<TFrom> src, Func<TFrom, TTarget> converter)
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
    
    internal StringView StringToNative(string src)
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
    
    private void* Alloc(int elementCount, int elementSize)
    {
        var size            = elementCount * elementSize;
        const int alignment = 8; // ensure 8 byte alignment
        var alignedPos      = (nativeMemoryPos + (alignment - 1)) & ~(alignment - 1);
        
        if (alignedPos + size <= NativeMemoryMax) {
            nativeMemoryPos = alignedPos + size;
            return nativeMemory + alignedPos;
        }
        var target = (byte*)NativeMemory.Alloc((nuint)elementCount, (nuint)elementSize);
        pointers.Add((nint)target);
        return target;
    }
    
    internal void Clear()
    {
        nativeMemoryPos = 0;
        foreach (var pointer in pointers) {
            NativeMemory.Free((void*)pointer);
        }
        pointers.Clear();
    }
}
