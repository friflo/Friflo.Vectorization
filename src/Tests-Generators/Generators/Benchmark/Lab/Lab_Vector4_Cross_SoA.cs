using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace Bench.Lab;

public static unsafe class Lab_Vector4_Cross_SoA
{
[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Cross8_Soa(
        // Left Vector Components
        float* axPtr, float* ayPtr, float* azPtr,
        // Right Vector Components
        float* bxPtr, float* byPtr, float* bzPtr,
        // Result Vector Components
        float* rxPtr, float* ryPtr, float* rzPtr)
    {
        // 1. LOAD: Direct 256-bit loads from contiguous arrays
        var ax = Avx.LoadVector256(axPtr);
        var ay = Avx.LoadVector256(ayPtr);
        var az = Avx.LoadVector256(azPtr);

        var bx = Avx.LoadVector256(bxPtr);
        var by = Avx.LoadVector256(byPtr);
        var bz = Avx.LoadVector256(bzPtr);

        // 2. MATH: Pure vertical operations (The "Ferrari" speed)
        // Res.x = ay * bz - az * by
        var rx = Avx.Subtract(Avx.Multiply(ay, bz), Avx.Multiply(az, by));
        
        // Res.y = az * bx - ax * bz
        var ry = Avx.Subtract(Avx.Multiply(az, bx), Avx.Multiply(ax, bz));
        
        // Res.z = ax * by - ay * bx
        var rz = Avx.Subtract(Avx.Multiply(ax, by), Avx.Multiply(ay, bx));

        // 3. STORE: Direct 256-bit stores back to contiguous arrays
        Avx.Store(rxPtr, rx);
        Avx.Store(ryPtr, ry);
        Avx.Store(rzPtr, rz);
    }
}