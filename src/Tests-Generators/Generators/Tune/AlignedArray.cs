using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tune;

public struct AlignedArray<T> where T : struct
{
    private readonly    T[]         _rawPinned;
    public              Memory<T>   Memory { get; }
    private readonly    int         offset;
    
    public Span<T>  Span => Memory.Span;
    
    public int Length => Memory.Length;
    
    public ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref _rawPinned[offset];
    }

    public AlignedArray(int length)
    {
        const int alignment = 32;
        int sizeOfT = Unsafe.SizeOf<T>();
        int padding = alignment / sizeOfT; 
        _rawPinned = GC.AllocateArray<T>(length + padding, pinned: true);


        IntPtr baseAddress = Marshal.UnsafeAddrOfPinnedArrayElement(_rawPinned, 0);
        
        long address = baseAddress;
        int offsetInBytes = (int)((alignment - (address % alignment)) % alignment);
        offset = offsetInBytes / sizeOfT;

        Memory = _rawPinned.AsMemory(offset, length);
    }
}