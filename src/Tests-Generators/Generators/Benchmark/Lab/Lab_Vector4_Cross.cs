using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace Bench.Lab;


public static unsafe class Lab_Vector4_Cross
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeCrossProduct8(Vector4* leftPtr, Vector4* rightPtr, Vector4* resultPtr)
    {
        // 1. LOAD: Grab 8 vectors from each input (256-bit contiguous)
        // vL01 contains Vector Left 0 and 1, etc.
        var vL01 = Avx.LoadVector256((float*)(leftPtr + 0));
        var vL23 = Avx.LoadVector256((float*)(leftPtr + 2));
        var vL45 = Avx.LoadVector256((float*)(leftPtr + 4));
        var vL67 = Avx.LoadVector256((float*)(leftPtr + 6));

        var vR01 = Avx.LoadVector256((float*)(rightPtr + 0));
        var vR23 = Avx.LoadVector256((float*)(rightPtr + 2));
        var vR45 = Avx.LoadVector256((float*)(rightPtr + 4));
        var vR67 = Avx.LoadVector256((float*)(rightPtr + 6));

        // 2. DEINTERLEAVE: Scramble into Even-Odd SoA
        // This puts X, Y, Z, W into separate registers in order [0, 2, 4, 6 | 1, 3, 5, 7]
        var (ax, ay, az, _) = Deinterleave8(vL01, vL23, vL45, vL67);
        var (bx, by, bz, _) = Deinterleave8(vR01, vR23, vR45, vR67);

        // 3. MATH: Vertical Cross Product
        // Result.x = ay * bz - az * by
        // Result.y = az * bx - ax * bz
        // Result.z = ax * by - ay * bx
        // Result.w = 0 (Standard for vector cross products)
        var rx = Avx.Subtract(Avx.Multiply(ay, bz), Avx.Multiply(az, by));
        var ry = Avx.Subtract(Avx.Multiply(az, bx), Avx.Multiply(ax, bz));
        var rz = Avx.Subtract(Avx.Multiply(ax, by), Avx.Multiply(ay, bx));
        var rw = Vector256<float>.Zero;

        // 4. INTERLEAVE & STORE: Scattered 128-bit exit
        InterleaveAndStore8(rx, ry, rz, rw, (float*)resultPtr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector256<float> X, Vector256<float> Y, Vector256<float> Z, Vector256<float> W) 
        Deinterleave8(Vector256<float> v01, Vector256<float> v23, Vector256<float> v45, Vector256<float> v67)
    {
        var xya = Avx.UnpackLow(v01, v23);  
        var zwa = Avx.UnpackHigh(v01, v23); 
        var xyb = Avx.UnpackLow(v45, v67);  
        var zwb = Avx.UnpackHigh(v45, v67); 

        var x = Vector256.AsSingle(Avx.UnpackLow (Vector256.AsDouble(xya), Vector256.AsDouble(xyb)));
        var y = Vector256.AsSingle(Avx.UnpackHigh(Vector256.AsDouble(xya), Vector256.AsDouble(xyb)));
        var z = Vector256.AsSingle(Avx.UnpackLow (Vector256.AsDouble(zwa), Vector256.AsDouble(zwb)));
        var w = Vector256.AsSingle(Avx.UnpackHigh(Vector256.AsDouble(zwa), Vector256.AsDouble(zwb)));
        return (x, y, z, w);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InterleaveAndStore8(Vector256<float> x, Vector256<float> y, Vector256<float> z, Vector256<float> w, float* destination)
    {
        var xyLo = Avx.UnpackLow(x, y);  
        var xyHi = Avx.UnpackHigh(x, y); 
        var zwLo = Avx.UnpackLow(z, w);  
        var zwHi = Avx.UnpackHigh(z, w); 

        var r0 = Vector256.AsSingle(Avx.UnpackLow (Vector256.AsDouble(xyLo), Vector256.AsDouble(zwLo)));
        var r1 = Vector256.AsSingle(Avx.UnpackHigh(Vector256.AsDouble(xyLo), Vector256.AsDouble(zwLo)));
        var r2 = Vector256.AsSingle(Avx.UnpackLow (Vector256.AsDouble(xyHi), Vector256.AsDouble(zwHi)));
        var r3 = Vector256.AsSingle(Avx.UnpackHigh(Vector256.AsDouble(xyHi), Vector256.AsDouble(zwHi)));

        // Scattered Store (128-bit) - Restores linear order
        Avx.Store(destination + 0,  Avx.ExtractVector128(r0, 0)); // V0
        Avx.Store(destination + 4,  Avx.ExtractVector128(r1, 0)); // V1
        Avx.Store(destination + 8,  Avx.ExtractVector128(r2, 0)); // V2
        Avx.Store(destination + 12, Avx.ExtractVector128(r3, 0)); // V3
        Avx.Store(destination + 16, Avx.ExtractVector128(r0, 1)); // V4
        Avx.Store(destination + 20, Avx.ExtractVector128(r1, 1)); // V5
        Avx.Store(destination + 24, Avx.ExtractVector128(r2, 1)); // V6
        Avx.Store(destination + 28, Avx.ExtractVector128(r3, 1)); // V7
    }
}