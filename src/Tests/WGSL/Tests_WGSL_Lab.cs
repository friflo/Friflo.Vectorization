using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CustomTypes;
using Friflo.Vectorization.WebGPU;
using Friflo.WGSL.Transpiler.WGSL;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable UseObjectOrCollectionInitializer
// ReSharper disable once InconsistentNaming
namespace Tests.WGSL;

public static class Tests_WGSL_Lab
{
    [Test]
    public static void Tests_WGSL_Lab_FixedSizeArray()
    {
        var array = new Vector2i_array_8();
        array[0] = new Vector2i(0, 42);
        array[7] = new Vector2i(7, 42);
        
        Assert.That(array.Length,   Is.EqualTo(8));
        Assert.That(array[0],       Is.EqualTo(new Vector2i(0, 42)));
        Assert.That(array[7],       Is.EqualTo(new Vector2i(7, 42)));


        int step = 0;
        foreach (ref var item in array)
        {
            switch (step) {
                case 0:  Assert.That(item, Is.EqualTo(new Vector2i(0, 42))); break;
                case 7:  Assert.That(item, Is.EqualTo(new Vector2i(7, 42))); break;
            }
            step++;
        }
        Assert.That(step, Is.EqualTo(8));
        
        var enumerator = array.GetEnumerator();
        var current = enumerator.Current;  // Direct call return first element
        Assert.That(current, Is.EqualTo(new Vector2i(0, 42)));
        
        while (enumerator.MoveNext()) {
            _ = enumerator.Current;
        }
        var last = enumerator.Current;
        Assert.That(last, Is.EqualTo(new Vector2i(7, 42)));
        
        Assert.Throws<IndexOutOfRangeException>(() => _ = array[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => _ = array[8]);
        
        
        var debugView = new FixedArrayDebugView<Vector2i>(array);
        var items = debugView.Items;
        Assert.That(items.Length, Is.EqualTo(8));
        Assert.That(items[0], Is.EqualTo(new Vector2i(0, 42)));
        Assert.That(items[7], Is.EqualTo(new Vector2i(7, 42)));
    }
    
    
    /// Fixed size array with 8 elements for use by a uniform with <see crefArrayStride.PadTo16Bytes40"/>
    [DebuggerTypeProxy(typeof(FixedArrayDebugView<Vector2i>))]
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct Vector2i_array_8
    {
        public int Length => 8;
        
        [FieldOffset(0)]  private Vector2i _element0; // size 8 byte. But <uniform> requires stride 16
        
        public ref Vector2i this[int index]
        {
            [UnscopedRef]
            get {
                if ((uint)index >= 8) throw new IndexOutOfRangeException();
                return ref Unsafe.AddByteOffset(ref _element0, (nint)index * 16);
            }
        }
        
        [UnscopedRef]
        public FixedArrayEnumerator<Vector2i> GetEnumerator() => new(ref _element0, 16, 128);
    }
}