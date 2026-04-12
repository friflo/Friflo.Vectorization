using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Bench.Lab;

public class Lab_Vector3_TransformAoS
{
    public static unsafe void TransformAoS(Vector3* p, int count, Matrix4x4 m)
    {
        // Load matrix rows into registers
        var row1 = Vector256.Create(m.M11, m.M12, m.M13, m.M14, m.M11, m.M12, m.M13, m.M14);
        var row2 = Vector256.Create(m.M21, m.M22, m.M23, m.M24, m.M21, m.M22, m.M23, m.M24);
        var row3 = Vector256.Create(m.M31, m.M32, m.M33, m.M34, m.M31, m.M32, m.M33, m.M34);

        for (int i = 0; i < count; i++)
        {
            // 2. Broadcast components for Dot Product
            // xxxx, yyyy, zzzz
            var xxxx = Avx.BroadcastScalarToVector128(&p[i].X);
            var yyyy = Avx.BroadcastScalarToVector128(&p[i].Y);
            var zzzz = Avx.BroadcastScalarToVector128(&p[i].Z);

            // 3. Perform the Horizontal Multiply-Add
            // Res = (xxxx * row1) + (yyyy * row2) + (zzzz * row3) + translation_row
            // Note: This is still slower than SoA because of the broadcasting/shuffling overhead
            var res = Sse.Add(
                Sse.Add(Sse.Multiply(xxxx, row1.GetLower()), Sse.Multiply(yyyy, row2.GetLower())),
                Sse.Add(Sse.Multiply(zzzz, row3.GetLower()), Vector128.Create(m.M14, m.M24, m.M34, 0.0f))
            );

            // 4. Store back
            p[i].X = res.GetElement(0);
            p[i].Y = res.GetElement(1);
            p[i].Z = res.GetElement(2);
        }
    }
}