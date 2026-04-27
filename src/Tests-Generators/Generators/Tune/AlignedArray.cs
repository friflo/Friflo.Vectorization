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
        this.Length = length;
        int sizeOfT = Unsafe.SizeOf<T>();
        
        // Wir brauchen Platz für (length * sizeOfT) Bytes. 
        // +31 Bytes Puffer, um sicher eine 32-Byte-Grenze zu finden.
        int totalBytes = (length * sizeOfT) + 31;
        int floatCount = (totalBytes + sizeof(float) - 1) / sizeof(float);
        
        // Pinned, damit der GC die Adresse nicht unter dem Hintern wegzieht
        _rawPinned = GC.AllocateArray<float>(floatCount, pinned: true);
        
        unsafe {
            fixed (float* ptr = _rawPinned) {
                long addr = (long)ptr;
                // Berechne wie viele BYTES wir überspringen müssen, um auf 0, 32, 64... zu kommen
                int byteSkip = (int)((32 - (addr & 31)) & 31);
                _floatOffset = byteSkip / sizeof(float);
                
                Debug.Assert(((addr + (long)_floatOffset * sizeof(float)) & 31) == 0, "SIMD Alignment failed!");
            }
        }
    }

    /// <summary>
    /// Das Herzstück für den Benchmark: Erzeugt einen perfekt ausgerichteten Span.
    /// </summary>
    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            ref float start = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_rawPinned), _floatOffset);
            return MemoryMarshal.CreateSpan(ref Unsafe.As<float, T>(ref start), Length);
        }
    }

    /// <summary>
    /// Schneller Indexer für Einzelzugriffe
    /// </summary>
    public ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref Unsafe.Add(ref Unsafe.As<float, T>(ref _rawPinned[_floatOffset]), index);
    }
}