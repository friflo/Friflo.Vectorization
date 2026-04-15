using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Bench.Lab;

public class Lab_Vector2_TransformSoA
{
    public static unsafe void TransformVector2_SoA(
        float* dataPtr,     // Pointer to the start of the X-lane
        int stride,         // The offset to the Y-lane (e.g., 1024)
        int count,          // Number of entities (must be multiple of 8 for this kernel)
        ref Matrix3x2 matrix)
    {
        // 1. Broadcast Matrix components to registers once
        var m11 = Vector256.Create(matrix.M11);
        var m12 = Vector256.Create(matrix.M12);
        var m21 = Vector256.Create(matrix.M21);
        var m22 = Vector256.Create(matrix.M22);
        var m31 = Vector256.Create(matrix.M31); // Translation X
        var m32 = Vector256.Create(matrix.M32); // Translation Y

        float* xLane = dataPtr;
        float* yLane = dataPtr + stride;

        // 2. The Shredder Loop
        for (int i = 0; i < count; i += 8)
        {
            // Load 8 X's and 8 Y's
            var x = Avx.LoadVector256(xLane + i);
            var y = Avx.LoadVector256(yLane + i);

            // New X = x * m11 + y * m21 + m31
            var resX = Fma.MultiplyAdd(x, m11, m31);
            resX = Fma.MultiplyAdd(y, m21, resX);

            // New Y = x * m12 + y * m22 + m32
            var resY = Fma.MultiplyAdd(x, m12, m32);
            resY = Fma.MultiplyAdd(y, m22, resY);

            // Store results back into the lanes
            Avx.Store(xLane + i, resX);
            Avx.Store(yLane + i, resY);
        }
    }
}