using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace Bench.Lab;


public static unsafe class Lab_Vector4_Cross_2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ComputeCrossProduct8_NoScatter(Vector4* leftPtr, Vector4* rightPtr, Vector4* resultPtr)
    {
        // 1. Contiguous 256-bit Loads
        var vL01 = Avx.LoadVector256((float*)(leftPtr + 0));
        var vL23 = Avx.LoadVector256((float*)(leftPtr + 2));
        var vL45 = Avx.LoadVector256((float*)(leftPtr + 4));
        var vL67 = Avx.LoadVector256((float*)(leftPtr + 6));

        var vR01 = Avx.LoadVector256((float*)(rightPtr + 0));
        var vR23 = Avx.LoadVector256((float*)(rightPtr + 2));
        var vR45 = Avx.LoadVector256((float*)(rightPtr + 4));
        var vR67 = Avx.LoadVector256((float*)(rightPtr + 6));

        // 2. Deinterleave (Even-Odd Scramble)
        var (ax, ay, az, aw) = Deinterleave8(vL01, vL23, vL45, vL67);
        var (bx, by, bz, bw) = Deinterleave8(vR01, vR23, vR45, vR67);

        // 3. Math (Stays the same)
        /* var rx = Avx.Subtract(Avx.Multiply(ay, bz), Avx.Multiply(az, by));
        var ry = Avx.Subtract(Avx.Multiply(az, bx), Avx.Multiply(ax, bz));
        var rz = Avx.Subtract(Avx.Multiply(ax, by), Avx.Multiply(ay, bx));
        var rw = Vector256<float>.Zero;*/
        /* var rx = ax + bx;
        var ry = ay + by;
        var rz = az + bz;
        var rw = aw + bw; */
        var rx = ax;
        var ry = ay;
        var rz = az;
        var rw = aw;
        

        // 4. Register-to-Register Interleave (The "Clean" Fix)
        // This assembles the registers into perfect [V0, V1], [V2, V3]... order
        var (v01, v23, v45, v67) = Interleave8_NoScatter(rx, ry, rz, rw);

        // 5. Contiguous 256-bit Stores
        Avx.Store((float*)(resultPtr + 0), v01);
        Avx.Store((float*)(resultPtr + 2), v23);
        Avx.Store((float*)(resultPtr + 4), v45);
        Avx.Store((float*)(resultPtr + 6), v67);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector256<float> v01, Vector256<float> v23, Vector256<float> v45, Vector256<float> v67) 
        Interleave8_NoScatter(Vector256<float> x, Vector256<float> y, Vector256<float> z, Vector256<float> w)
    {
        // Stage A: In-lane component shuffle
        var xyLo = Avx.UnpackLow(x, y);  // [X0 Y0 X4 Y4 | X1 Y1 X5 Y5]
        var xyHi = Avx.UnpackHigh(x, y); 
        var zwLo = Avx.UnpackLow(z, w);  
        var zwHi = Avx.UnpackHigh(z, w); 

        // Stage B: Re-bundle into V-halves
        var r0 = Vector256.AsSingle(Avx.UnpackLow (Vector256.AsDouble(xyLo), Vector256.AsDouble(zwLo)));
        var r1 = Vector256.AsSingle(Avx.UnpackHigh(Vector256.AsDouble(xyLo), Vector256.AsDouble(zwLo)));
        var r2 = Vector256.AsSingle(Avx.UnpackLow (Vector256.AsDouble(xyHi), Vector256.AsDouble(zwHi)));
        var r3 = Vector256.AsSingle(Avx.UnpackHigh(Vector256.AsDouble(xyHi), Vector256.AsDouble(zwHi)));

        // Stage C: Cross-lane assembly using Permute2x128
        // This pairs V0 with V1, and V4 with V5, etc.
        var v01_raw = Avx.Permute2x128(r0, r1, 0x20); 
        var v45_raw = Avx.Permute2x128(r0, r1, 0x31);
        var v23_raw = Avx.Permute2x128(r2, r3, 0x20);
        var v67_raw = Avx.Permute2x128(r2, r3, 0x31);

        // Stage D: Final Lane Fix (Permute4x64)
        // Corrects the [V_low, V_low | V_high, V_high] order to [V_low, V_high | V_low, V_high]
        // 0xD8 = 11 01 10 00 (Standard shuffle for correcting UnpackLow/High pairs)
        return (
            Avx2.Permute4x64(v01_raw.AsInt64(), 0xD8).AsSingle(),
            Avx2.Permute4x64(v23_raw.AsInt64(), 0xD8).AsSingle(),
            Avx2.Permute4x64(v45_raw.AsInt64(), 0xD8).AsSingle(),
            Avx2.Permute4x64(v67_raw.AsInt64(), 0xD8).AsSingle()
        );
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
}