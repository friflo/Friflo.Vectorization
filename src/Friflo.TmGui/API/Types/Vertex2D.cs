// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable SuggestVarOrType_Elsewhere

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
public struct Vertex2D
{
    public  Vector2 position;   // 8
    public  Vector2 uv;         // 8
    public  uint    color;      // 4 (Rgba8Pack)

    public override string ToString() => position.ToString();

    public Vertex2D(Vector2 position, Vector2 uv, Color32 color)
    {
        this.position = position;
        this.uv = uv;
        this.color = color;
    }
}

/// <summary>
/// [0] Top-Left   [1] Top-Right   [2] Bottom-Right   [3] Bottom-Left
/// </summary>
[InlineArray(4)]
public struct VertexQuad : IEquatable<VertexQuad>
{
    private Vertex2D _element0;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override int GetHashCode()
    {
        ref byte bytePtr = ref Unsafe.As<VertexQuad, byte>(ref Unsafe.AsRef(in this));
        ref ulong ptr = ref Unsafe.As<byte, ulong>(ref bytePtr);

        // WyHash / xxHash3 style mixing with full 128-bit multiplications
        ulong hash = FastMix(Unsafe.Add(ref ptr, 0) ^ 0x9E3779B97F4A7C15UL, Unsafe.Add(ref ptr, 1) ^ 0xBF58476D1CE4E5B9UL)
                   ^ FastMix(Unsafe.Add(ref ptr, 2) ^ 0x94D049BB133111EBUL, Unsafe.Add(ref ptr, 3) ^ 0x41C64E6D9625C371UL)
                   ^ FastMix(Unsafe.Add(ref ptr, 4) ^ 0xA0761D6478BD642FUL, Unsafe.Add(ref ptr, 5) ^ 0xE7037ED1A0B428DBUL)
                   ^ FastMix(Unsafe.Add(ref ptr, 6) ^ 0x8EBC6AF09C88C6E3UL, Unsafe.Add(ref ptr, 7) ^ 0x589965CC75374CC3UL)
                   ^ FastMix(Unsafe.Add(ref ptr, 8) ^ 0x1D8E4E27C47D124FUL, Unsafe.Add(ref ptr, 9) ^ 0x27BB2EE687B0B0FDUL)
                   ^ 80UL; // 80 bytes length seed

        // Final avalanche mixer (MurmurHash3 / WyHash finalizer)
        hash ^= hash >> 33;
        hash *= 0xFF51AFD7ED558CCDUL;
        hash ^= hash >> 33;
        hash *= 0xC4CEB9FE1A85EC53UL;
        hash ^= hash >> 33;

        return (int)hash;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong FastMix(ulong a, ulong b)
    {
        ulong low = Math.BigMul(a, b, out ulong high);
        return low ^ high;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong FastMix_Old(ulong v1, ulong v2)
    {
        ulong p = v1 * v2;
        return p ^ (p >> 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetHashCode_Old()
    {
        ref byte bytePtr = ref Unsafe.As<VertexQuad, byte>(ref Unsafe.AsRef(in this));

        // 1. AVX2 / 256-Bit Path (x86-64)
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<ulong> primes256 = Vector256.Create(
                0x9E3779B97F4A7C15UL, 0xBF58476D1CE4E5B9UL,
                0x94D049BB133111EBUL, 0x9E3779B97F4A7C15UL);

            Vector128<ulong> primes128 = Vector128.Create(
                0xBF58476D1CE4E5B9UL, 0x94D049BB133111EBUL);

            // Read 80 bytes: 32B + 32B + 16B
            Vector256<ulong> v0 = Vector256.LoadUnsafe(ref bytePtr, 0).AsUInt64() ^ primes256;
            Vector256<ulong> v1 = Vector256.LoadUnsafe(ref bytePtr, 32).AsUInt64() ^ primes256;
            Vector128<ulong> v2 = Vector128.LoadUnsafe(ref bytePtr, 64).AsUInt64() ^ primes128;

            // Reduce Vector256 blocks to Vector128 via XOR
            Vector128<ulong> red0 = v0.GetLower() ^ v0.GetUpper();
            Vector128<ulong> red1 = v1.GetLower() ^ v1.GetUpper();

            Vector128<ulong> combined = red0 ^ red1 ^ v2;

            ulong mix = FastMix_Old(combined.GetElement(0), combined.GetElement(1));
            return (int)(mix ^ (mix >> 32));
        }
        // 2. ARM NEON / SSE2 / 128-Bit Path
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<ulong> primes128A = Vector128.Create(
                0x9E3779B97F4A7C15UL, 0xBF58476D1CE4E5B9UL);

            Vector128<ulong> primes128B = Vector128.Create(
                0x94D049BB133111EBUL, 0x9E3779B97F4A7C15UL);

            // Read 80 bytes as 5 x 16-byte vectors
            Vector128<ulong> v0 = Vector128.LoadUnsafe(ref bytePtr, 0).AsUInt64() ^ primes128A;
            Vector128<ulong> v1 = Vector128.LoadUnsafe(ref bytePtr, 16).AsUInt64() ^ primes128B;
            Vector128<ulong> v2 = Vector128.LoadUnsafe(ref bytePtr, 32).AsUInt64() ^ primes128A;
            Vector128<ulong> v3 = Vector128.LoadUnsafe(ref bytePtr, 48).AsUInt64() ^ primes128B;
            Vector128<ulong> v4 = Vector128.LoadUnsafe(ref bytePtr, 64).AsUInt64() ^ primes128A;

            // Fold all 128-bit blocks into a single vector
            Vector128<ulong> combined = v0 ^ v1 ^ v2 ^ v3 ^ v4;

            ulong mix = FastMix_Old(combined.GetElement(0), combined.GetElement(1));
            return (int)(mix ^ (mix >> 32));
        }

        // 3. Scalar Fallback
        ref ulong ptr = ref Unsafe.As<byte, ulong>(ref bytePtr);

        const ulong prime1 = 0x9E3779B97F4A7C15UL;
        const ulong prime2 = 0xBF58476D1CE4E5B9UL;
        const ulong prime3 = 0x94D049BB133111EBUL;

        ulong h0 = Unsafe.Add(ref ptr, 0) ^ prime1;
        ulong h1 = Unsafe.Add(ref ptr, 1) ^ prime2;
        ulong h2 = Unsafe.Add(ref ptr, 2) ^ prime3;
        ulong h3 = Unsafe.Add(ref ptr, 3) ^ prime1;
        ulong h4 = Unsafe.Add(ref ptr, 4) ^ prime2;
        ulong h5 = Unsafe.Add(ref ptr, 5) ^ prime3;
        ulong h6 = Unsafe.Add(ref ptr, 6) ^ prime1;
        ulong h7 = Unsafe.Add(ref ptr, 7) ^ prime2;
        ulong h8 = Unsafe.Add(ref ptr, 8) ^ prime3;
        ulong h9 = Unsafe.Add(ref ptr, 9) ^ prime1;

        ulong mixA = FastMix_Old(h0 ^ h3, h1 ^ h4) ^ h2;
        ulong mixB = FastMix_Old(h5 ^ h8, h6 ^ h9) ^ h7;

        ulong finalHash = FastMix_Old(mixA, mixB);
        return (int)(finalHash ^ (finalHash >> 32));
    }



    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool Equals(VertexQuad other)
    {
        ReadOnlySpan<byte> thisBytes = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<VertexQuad, byte>(ref Unsafe.AsRef(in this)), 80);
        ReadOnlySpan<byte> otherBytes = MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<VertexQuad, byte>(ref Unsafe.AsRef(in other)), 80);

        return thisBytes.SequenceEqual(otherBytes);
    }

    public readonly override bool Equals(object? obj) => obj is VertexQuad other && Equals(other);

    public static bool operator ==(in VertexQuad left, in VertexQuad right) => left.Equals(right);
    public static bool operator !=(in VertexQuad left, in VertexQuad right) => !left.Equals(right);
}