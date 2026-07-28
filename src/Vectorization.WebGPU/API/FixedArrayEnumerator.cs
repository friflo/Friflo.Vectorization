// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public ref struct FixedArrayEnumerator<T>  where T : unmanaged
{
    private             int     offset;     //  4
    private readonly    int     stride;     //  4
    private readonly    int     size;       //  4
    private readonly    ref T   element0;
    
    public FixedArrayEnumerator(ref T element0, int stride, int size) {
        offset          = -stride;
        this.stride     =  stride;
        this.size       =  size;
        this.element0   =  ref element0;
    }
    
    public ref T Current => ref Unsafe.AddByteOffset(ref element0, offset < 0 ? 0 : offset);
    
    public bool MoveNext() {
        int nextOffset = offset + stride;
        if (nextOffset < size) {
            offset = nextOffset;
            return true;
        }
        return false;
    }

    public void Reset() {
        offset = -stride;
    }
}

public class FixedArrayDebugView<T> where T : unmanaged
{
    private delegate ref T IndexerDelegate<TStruct>(ref TStruct instance, int index);

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items { get; }

    public FixedArrayDebugView(object target)
    {
        Items = GetItems(target);
    }
    
    private static T[] GetItems(object target)
    {
        var getItemMethod = FixedArrayDebugViewUtils.GetGetItemMethod(target, out var type, out var count);
        if (getItemMethod == null) {
            return [];
        }
        // adjust generic method to concrete type
        var helperMethod = typeof(FixedArrayDebugView<T>)
            .GetMethod(nameof(GetItemsGeneric), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type);

        return (T[])helperMethod.Invoke(null, [target, getItemMethod, count])!;
    }

    private static T[] GetItemsGeneric<TStruct>(object target, MethodInfo getItemMethod, int count) 
        where TStruct : struct
    {
        var typedTarget = (TStruct)target;

        var indexer = (IndexerDelegate<TStruct>)Delegate.CreateDelegate(typeof(IndexerDelegate<TStruct>), null, getItemMethod);
        var items = new T[count];
        for (int i = 0; i < count; i++) {
            items[i] = indexer(ref typedTarget, i);
        }
        return items;
    }
}

internal static class FixedArrayDebugViewUtils
{
    internal static MethodInfo GetGetItemMethod(object target, out Type type, out int count)
    {
        count = 0;
        type = null;
        if (target == null) {
            return null;
        }
        type = target.GetType();
        
        var lengthField = type.GetField("Length", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (lengthField == null) {
            return null;
        }
        count = (int)lengthField.GetValue(target)!;
        if (count == 0) {
            return null;
        }
        return type.GetMethod("get_Item", BindingFlags.Public | BindingFlags.Instance);
    } 
}