using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Bench.Lab;

public class Lab_Vector3_TransformSoA
{
    public static unsafe void TransformSoA(
        float* pX, float* pY, float* pZ, 
        int count, Matrix4x4 m)
    {
        // Load matrix rows into 8-lane registers
        var m11 = Vector256.Create(m.M11); var m12 = Vector256.Create(m.M12);
        var m13 = Vector256.Create(m.M13); var m14 = Vector256.Create(m.M14);
        
        var m21 = Vector256.Create(m.M21); var m22 = Vector256.Create(m.M22);
        var m23 = Vector256.Create(m.M23); var m24 = Vector256.Create(m.M24);
        
        var m31 = Vector256.Create(m.M31); var m32 = Vector256.Create(m.M32);
        var m33 = Vector256.Create(m.M33); var m34 = Vector256.Create(m.M34);

        // Process 8 entities at a time
        for (int i = 0; i < count; i += 8)
        {
            var vx = Avx.LoadVector256(pX + i);
            var vy = Avx.LoadVector256(pY + i);
            var vz = Avx.LoadVector256(pZ + i);

            // Vertical SIMD using FMA: (A * B) + C
            // X' = x*m11 + y*m12 + z*m13 + m14
            var ox = Fma.MultiplyAdd(vx, m11, m14);     // ox = vx * m11 + m14
            ox = Fma.MultiplyAdd(vy, m12, ox);          // ox = vy * m12 + ox
            ox = Fma.MultiplyAdd(vz, m13, ox);          // ox = vz * m13 + ox

            // Y' = x*m21 + y*m22 + z*m23 + m24
            var oy = Fma.MultiplyAdd(vx, m21, m24);
            oy = Fma.MultiplyAdd(vy, m22, oy);
            oy = Fma.MultiplyAdd(vz, m23, oy);

            // Z' = x*m31 + y*m32 + z*m33 + m34
            var oz = Fma.MultiplyAdd(vx, m31, m34);
            oz = Fma.MultiplyAdd(vy, m32, oz);
            oz = Fma.MultiplyAdd(vz, m33, oz);

            Avx.Store(pX + i, ox);
            Avx.Store(pY + i, oy);
            Avx.Store(pZ + i, oz);
        }
    }
}