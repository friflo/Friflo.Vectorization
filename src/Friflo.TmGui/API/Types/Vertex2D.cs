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

    public override int GetHashCode() => GetQuadHash();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly int GetQuadHash()
    {
        ref byte bytePtr = ref Unsafe.As<VertexQuad, byte>(ref Unsafe.AsRef(in this));
        /*
        // 1. AVX2 / 256-Bit Path (3 fast vector loads for 80 bytes)
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<ulong> p0 = Vector256.Create(0x9E3779B97F4A7C15UL, 0xBF58476D1CE4E5B9UL, 0x94D049BB133111EBUL, 0x41C64E6D9625C371UL);
            Vector256<ulong> p1 = Vector256.Create(0xA0761D6478BD642FUL, 0xE7037ED1A0B428DBUL, 0x8EBC6AF09C88C6E3UL, 0x589965CC75374CC3UL);
            Vector128<ulong> p2 = Vector128.Create(0x1D8E4E27C47D124FUL, 0x27BB2EE687B0B0FDUL);

            Vector256<ulong> v0 = Vector256.LoadUnsafe(ref bytePtr, 0).AsUInt64() ^ p0;
            Vector256<ulong> v1 = Vector256.LoadUnsafe(ref bytePtr, 32).AsUInt64() ^ p1;
            Vector128<ulong> v2 = Vector128.LoadUnsafe(ref bytePtr, 64).AsUInt64() ^ p2;

            // Bit-rotations per block force floating-point diffs to land on completely different bit positions
            ulong r0 = BitOperations.RotateLeft(v0.GetLower().GetElement(0), 13) ^ v0.GetLower().GetElement(1);
            ulong r1 = BitOperations.RotateLeft(v1.GetLower().GetElement(0), 27) ^ v1.GetLower().GetElement(1);
            ulong r2 = BitOperations.RotateLeft(v0.GetUpper().GetElement(0), 41) ^ v0.GetUpper().GetElement(1);
            ulong r3 = BitOperations.RotateLeft(v1.GetUpper().GetElement(0), 55) ^ v1.GetUpper().GetElement(1);

            ulong foldA = r0 ^ r3 ^ v2.GetElement(0);
            ulong foldB = r1 ^ r2 ^ v2.GetElement(1);

            ulong mix = FastMix(foldA, foldB);
            return (int)(mix ^ (mix >> 32));
        } */

        ref ulong ptr = ref Unsafe.As<byte, ulong>(ref bytePtr);

        ulong h0 = Unsafe.Add(ref ptr, 0) ^ 0x9E3779B97F4A7C15UL;
        ulong h1 = Unsafe.Add(ref ptr, 1) ^ 0xBF58476D1CE4E5B9UL;
        ulong h2 = Unsafe.Add(ref ptr, 2) ^ 0x94D049BB133111EBUL;
        ulong h3 = Unsafe.Add(ref ptr, 3) ^ 0x41C64E6D9625C371UL;
        ulong h4 = Unsafe.Add(ref ptr, 4) ^ 0xA0761D6478BD642FUL;
        ulong h5 = Unsafe.Add(ref ptr, 5) ^ 0xE7037ED1A0B428DBUL;
        ulong h6 = Unsafe.Add(ref ptr, 6) ^ 0x8EBC6AF09C88C6E3UL;
        ulong h7 = Unsafe.Add(ref ptr, 7) ^ 0x589965CC75374CC3UL;
        ulong h8 = Unsafe.Add(ref ptr, 8) ^ 0x1D8E4E27C47D124FUL;
        ulong h9 = Unsafe.Add(ref ptr, 9) ^ 0x27BB2EE687B0B0FDUL;

        // Rotate individual 64-bit words by prime offsets to destroy vertex-alignment symmetry
        ulong mixA = FastMix(h0 ^ BitOperations.RotateLeft(h3, 17) ^ BitOperations.RotateLeft(h6, 31), 
                             h1 ^ BitOperations.RotateLeft(h4, 23) ^ BitOperations.RotateLeft(h7, 47));
        ulong mixB = FastMix(h2 ^ BitOperations.RotateLeft(h5, 19) ^ BitOperations.RotateLeft(h8, 37), 
                             h9 ^ 0x9E3779B97F4A7C15UL);

        ulong finalHash = FastMix(mixA, mixB);
        return (int)(finalHash ^ (finalHash >> 32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong FastMix(ulong a, ulong b)
    {
        ulong low = Math.BigMul(a | 1UL, b | 1UL, out ulong high);
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