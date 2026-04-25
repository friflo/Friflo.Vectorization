using System;
using System.Runtime.InteropServices;

namespace Tune;

public struct AlignedArray
{
    private readonly    float[]         _rawPinned;
    public              Memory<float>   Memory { get; }
    
    public Span<float>  Span => Memory.Span;

    public AlignedArray(int length)
    {
        const int alignment = 32;
        const int floatSize = 4;
        
        int padding = alignment / floatSize; 
        _rawPinned = GC.AllocateArray<float>(length + padding, pinned: true);


        IntPtr baseAddress = Marshal.UnsafeAddrOfPinnedArrayElement(_rawPinned, 0);
        
        long address = baseAddress;
        int offsetInBytes = (int)((alignment - (address % alignment)) % alignment);
        int offsetInFloats = offsetInBytes / floatSize;

        Memory = _rawPinned.AsMemory(offsetInFloats, length);
    }
}