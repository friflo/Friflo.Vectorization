using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Bench;
using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Friflo.Vectorization;
using Friflo.Vectorization.Intrinsics;
using NUnit.Framework;
using Tests.ECS;

// ReSharper disable InconsistentNaming
namespace Tune;

[BenchmarkCategory("Vector2")]
[MemoryDiagnoser] // Tracks GC allocations
public partial class Tune_Vector2
{
    private EntityStore store;
    const int EntityCount = Constants.EntityCount;

    private Matrix4x4   matrix4x4;
    
    [GlobalSetup]
    [SetUp]
    public void Setup() {
        store = new EntityStore();
        for (int n = 0; n < EntityCount; n++) {
            store.CreateEntity(
                new Position2 { value = new Vector2(n,n)},
                new Velocity2 { value = new Vector2(1,2)},
                new FloatComponent { value = n },
                new Pos2SoA { value = new Vector2(n, n + 10_000)}
                );
        }
        Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(
            10f * (MathF.PI / 180.0f), // Yaw
            20f * (MathF.PI / 180.0f), // Pitch
            30f * (MathF.PI / 180.0f)  // Roll
        );
        Matrix4x4 trans = Matrix4x4.CreateTranslation(new Vector3(1f, 2f, 3f));
        matrix4x4 = Matrix4x4.Multiply(rot, trans);
    }
    
    // ---------------------------------------------------------------------------
    [Vectorize(nameof(TransformRhythm))][Query]  [OmitHash]
    private static void TransformMatrix4x4_AoSoA(ref Pos2SoA position, Matrix4x4 matrix) {
        position.value = Vector2.Transform(position.value, matrix);
    }
    
    [Benchmark] [Test] // dotnet run -c Release --filter *Tune_Vector2.Vector2_TransformRhythm*
    public void Vector2_TransformRhythm() {
        TransformMatrix4x4_AoSoAQuery(store, matrix4x4);
    }
    
    // - Broadcast matrix vector at method head
    // - Interleaved Load/Comput/Store blocks
    // - Used LoadAlignedVector256() instead of LoadVector256()
    // - (most significant) Used distinct local variables for the second half of the unroll to enable Out-of-Order Execution
    [SkipLocalsInit]
    private static unsafe int TransformRhythm(int count, Span<float> position, Matrix4x4 matrix)
    {
        int paddedCount = (count + 15) & ~15;
        int i = 0;
        if (position.Length < paddedCount) VectorUtils.ThrowBufferTooSmall(nameof(position));

        // --- Pre-Broadcast Matrix Elements to Registers (The "Fat" setup)
        var m11 = Vector256.Create(matrix.M11);         // vbroadcastss ymm1, [mem]
        var m12 = Vector256.Create(matrix.M12);         // vbroadcastss ymm2, [mem]
        var m21 = Vector256.Create(matrix.M21);         // vbroadcastss ymm3, [mem]
        var m22 = Vector256.Create(matrix.M22);         // vbroadcastss ymm4, [mem]
        var m41 = Vector256.Create(matrix.M41);         // vbroadcastss ymm5, [mem]
        var m42 = Vector256.Create(matrix.M42);         // vbroadcastss ymm10, [mem]

        fixed (float* position_first = position)
        {
            float* pPtr = (float*)position_first;

            for (; i < paddedCount; i += 16)
            {
                // --- BLOCK 1: Load First Half ---
                var x0 = Avx.LoadAlignedVector256(pPtr);        // vmovaps ymm6, [pPtr]
                var y0 = Avx.LoadAlignedVector256(pPtr + 8);    // vmovaps ymm7, [pPtr + 32]

                // --- BLOCK 2: Math First Half (Interleaved) ---
                // x' = x*m11 + y*m21 + m41
                var resX0 = Fma.MultiplyAdd(x0, m11, m41);      // vfmadd213ps ymm6, ymm1, ymm5
                resX0 = Fma.MultiplyAdd(y0, m21, resX0);        // vfmadd231ps ymm6, ymm7, ymm3
                
                // y' = x*m12 + y*m22 + m42
                var resY0 = Fma.MultiplyAdd(x0, m12, m42);      // vfmadd213ps ymm11, ymm2, ymm10
                resY0 = Fma.MultiplyAdd(y0, m22, resY0);        // vfmadd231ps ymm11, ymm7, ymm4

                // --- BLOCK 3: Load Second Half (Hiding Math A Latency) ---
                var x1 = Avx.LoadAlignedVector256(pPtr + 16);   // vmovaps ymm8, [pPtr + 64]
                var y1 = Avx.LoadAlignedVector256(pPtr + 24);   // vmovaps ymm9, [pPtr + 96]

                // --- BLOCK 4: Store First Half ---
                Avx.StoreAligned(pPtr, resX0);                  // vmovaps [pPtr], ymm6
                Avx.StoreAligned(pPtr + 8, resY0);              // vmovaps [pPtr + 32], ymm11

                // --- BLOCK 5: Math Second Half ---
                var resX1 = Fma.MultiplyAdd(x1, m11, m41);      // vfmadd213ps ymm8, ymm1, ymm5
                resX1 = Fma.MultiplyAdd(y1, m21, resX1);        // vfmadd231ps ymm8, ymm9, ymm3

                var resY1 = Fma.MultiplyAdd(x1, m12, m42);      // vfmadd213ps ymm12, ymm2, ymm10
                resY1 = Fma.MultiplyAdd(y1, m22, resY1);        // vfmadd231ps ymm12, ymm9, ymm4

                // --- BLOCK 6: Store Second Half ---
                Avx.StoreAligned(pPtr + 16, resX1);             // vmovaps [pPtr + 64], ymm8
                Avx.StoreAligned(pPtr + 24, resY1);             // vmovaps [pPtr + 96], ymm12

                pPtr += 32;                                     // add rdi, 128
            }
        }
        return i;
    }
 
}
