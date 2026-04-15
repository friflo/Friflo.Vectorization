using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Bench.Lab;

public class Lab_Vector2_TransformAoS
{
    public static unsafe void TransformVector2_AoS(
        float* dataPtr,     // Pointer to [x, y, x, y, x, y, x, y...]
        int count,          // Total number of Vector2s (entities)
        ref Matrix3x2 matrix)
    {
        // 1. Broadcast matrix components
        var m11 = Vector256.Create(matrix.M11);
        var m12 = Vector256.Create(matrix.M12);
        var m21 = Vector256.Create(matrix.M21);
        var m22 = Vector256.Create(matrix.M22);
        var m31 = Vector256.Create(matrix.M31);
        var m32 = Vector256.Create(matrix.M32);

        for (int i = 0; i < count; i += 4)
        {
            // 2. Load 8 floats (representing 4 Vector2s: x0, y0, x1, y1, x2, y2, x3, y3)
            Vector256<float> chunk = Avx.LoadVector256(dataPtr + (i * 2));

            // 3. THE SHUFFLE TAX: We need to separate X's and Y's
            // We use a shuffle mask to get [x0, x1, x2, x3, x0, x1, x2, x3] roughly
            // This usually requires two instructions to get clean 'x' and 'y' registers
            Vector256<float> x = Avx.Shuffle(chunk, chunk, 0b10_00_10_00); // [x0, x1, x0, x1, x2, x3, x2, x3]
            Vector256<float> y = Avx.Shuffle(chunk, chunk, 0b11_01_11_01); // [y0, y1, y0, y1, y2, y3, y2, y3]
            
            // Note: Actual 256-bit shuffles are "lane-bound," making this even messier
            // and often requiring PermuteVar8x32 to get data across the 128-bit boundary.

            // 4. Perform the Math
            var resX = Fma.MultiplyAdd(x, m11, m31);
            resX = Fma.MultiplyAdd(y, m21, resX);

            var resY = Fma.MultiplyAdd(x, m12, m32);
            resY = Fma.MultiplyAdd(y, m22, resY);

            // 5. THE UN-SHUFFLE TAX: Now we have to interleave them back!
            // [x0, x1, x2, x3] + [y0, y1, y2, y3] -> [x0, y0, x1, y1...]
            Vector256<float> finalChunk = Avx.UnpackLow(resX, resY); // Interleave low parts
            // ... and high parts, then blend...
            
            Avx.Store(dataPtr + (i * 2), finalChunk);
        }
    }
}