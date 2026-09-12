// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


internal static class HashUtils
{
    private static readonly ulong[] SecretKey =
    [
        0xb8fe6c3923a44bbeUL, 0x7c015d3080eede50UL,
        0x402927299a9442b1UL, 0xd7b58e393992440bUL
    ];

    // Alternative to:  System.IO.Hashing.XxHash3.HashToUInt64()
    // data.Length should be >= 64
    internal static ulong Hash(ReadOnlySpan<byte> data)
    {
        int length = data.Length;
        int index = 0;

        // 1. Path for 256-Bit Hardware (e.g. x86-64 with AVX2)
        if (Vector256.IsHardwareAccelerated && length >= 64)
        {
            ref byte ptr = ref MemoryMarshal.GetReference(data);
            ref byte keyPtr = ref Unsafe.As<ulong, byte>(ref SecretKey[0]);

            Vector256<ulong> keyVec = Unsafe.ReadUnaligned<Vector256<ulong>>(ref keyPtr);
            Vector256<ulong> acc = Vector256<ulong>.Zero;

            int limit = length - 64;

            while (index <= limit)
            {
                Vector256<ulong> dataA = Unsafe.ReadUnaligned<Vector256<ulong>>(ref Unsafe.Add(ref ptr, index));
                Vector256<ulong> dataB = Unsafe.ReadUnaligned<Vector256<ulong>>(ref Unsafe.Add(ref ptr, index + 32));

                Vector256<ulong> keyedA = dataA ^ keyVec;
                Vector256<ulong> keyedB = dataB ^ keyVec;

                Vector256<uint> lowA = keyedA.AsUInt32();
                Vector256<uint> lowB = keyedB.AsUInt32();

                Vector256<uint> shiftedA = Vector256.ShiftRightLogical(lowA, 32);
                Vector256<uint> shiftedB = Vector256.ShiftRightLogical(lowB, 32);

                Vector256<ulong> productA = Avx2.IsSupported 
                    ? Avx2.Multiply(lowA, shiftedA) 
                    : (keyedA * keyVec);

                Vector256<ulong> productB = Avx2.IsSupported 
                    ? Avx2.Multiply(lowB, shiftedB) 
                    : (keyedB * keyVec);

                acc += productA + productB;
                index += 64;
            }

            ulong result = acc.GetElement(0) ^ acc.GetElement(1) ^ acc.GetElement(2) ^ acc.GetElement(3);
            return FoldAvalanche(result, (ulong)length);
        }
        // 2. Fallback Path for 128-Bit Hardware (e.g. ARM64 / NEON / SSE2)
        else if (Vector128.IsHardwareAccelerated && length >= 32)
        {
            ref byte ptr = ref MemoryMarshal.GetReference(data);
            ref byte keyPtr = ref Unsafe.As<ulong, byte>(ref SecretKey[0]);

            Vector128<ulong> keyVec = Unsafe.ReadUnaligned<Vector128<ulong>>(ref keyPtr);
            Vector128<ulong> acc = Vector128<ulong>.Zero;

            int limit = length - 32;

            while (index <= limit)
            {
                Vector128<ulong> dataA = Unsafe.ReadUnaligned<Vector128<ulong>>(ref Unsafe.Add(ref ptr, index));
                Vector128<ulong> keyedA = dataA ^ keyVec;

                Vector128<uint> lowA = keyedA.AsUInt32();
                Vector128<uint> shiftedA = Vector128.ShiftRightLogical(lowA, 32);

                Vector128<ulong> productA = Sse2.IsSupported
                    ? Sse2.Multiply(lowA, shiftedA)
                    : (keyedA * keyVec);

                acc += productA;
                index += 32;
            }

            ulong result = acc.GetElement(0) ^ acc.GetElement(1);
            return FoldAvalanche(result, (ulong)length);
        }

        // 3. Scalar Fallback
        return FastScalarHash(data);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong FoldAvalanche(ulong acc, ulong len)
    {
        acc ^= acc >> 33;
        acc *= 0xc2b2ae3d27d4eb4fUL;
        acc ^= acc >> 29;
        acc *= 0x165667b19e3779f9UL;
        acc ^= acc >> 32;
        return acc ^ len;
    }

    private static ulong FastScalarHash(ReadOnlySpan<byte> data)
    {
        ulong h = (ulong)data.Length * 0x9e3779b97f4a7c15UL;
        foreach (byte b in data)
        {
            h = (h ^ b) * 0xbf58476d1ce4e5b9UL;
        }
        return h;
    }
}