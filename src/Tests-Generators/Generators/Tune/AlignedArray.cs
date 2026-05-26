using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Tune;

public struct AlignedArray<T> where T : struct
{
    private readonly float[] _rawPinned;
    private readonly int     _floatOffset;
    public  readonly int     Length;

    public AlignedArray(int length) {
        Length = length;
        int sizeOfT = Unsafe.SizeOf<T>();
        
        // add additional 31 bytes to enable offsetting
        int totalBytes = (length * sizeOfT) + 31;
        int floatCount = (totalBytes + sizeof(float) - 1) / sizeof(float);
        
        // Pinned, to prevent GC moving the memory
        _rawPinned = GC.AllocateArray<float>(floatCount, pinned: true);
        
        unsafe {
            fixed (float* ptr = _rawPinned) {
                long addr = (long)ptr;
                // calculate how many bytes need to be skipped to get alignment: 0, 32, 64... 
                int byteSkip = (int)((32 - (addr & 31)) & 31);
                _floatOffset = byteSkip / sizeof(float);
                
                Debug.Assert(((addr + (long)_floatOffset * sizeof(float)) & 31) == 0, "SIMD Alignment failed!");
            }
        }
    }

    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            ref float start = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_rawPinned), _floatOffset);
            return MemoryMarshal.CreateSpan(ref Unsafe.As<float, T>(ref start), Length);
        }
    }

    public ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref Unsafe.As<float, T>(ref _rawPinned[_floatOffset]), index);
    }
}