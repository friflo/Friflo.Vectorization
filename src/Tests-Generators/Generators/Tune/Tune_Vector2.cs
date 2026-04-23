using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Bench;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Friflo.Engine.ECS;
using Friflo.Vectorization;
using Friflo.Vectorization.Intrinsics;
using NUnit.Framework;
using Tests.ECS;

// ReSharper disable InconsistentNaming
namespace Tune;

[BenchmarkCategory("Vector2")]
[MemoryDiagnoser] // Tracks GC allocations
// [Config(typeof(Config))]
public partial class Tune_Vector2
{
    private class Config : ManualConfig
    {
        public Config() 
        {
            // Add the standard columns + our new Comment column
            AddColumn(new CommentColumn()); 
        } 
    }
    
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
    
    [Benchmark] [Test]
    public void Vector2_TransformMatrix4x4_AoSoA_Vectorized() {
        TransformMatrix4x4_AoSoAQuery(store, matrix4x4);
    }
    
    // [Layout: AoS-SoA-Mixed] - lane-native speed + Deinterleave penalty
    [SkipLocalsInit]
    private static unsafe int TransformRhythm(int count, Span<float> position, Matrix4x4 matrix)
    {
        int paddedCount = (count + 15) & ~15;
        int i = 0;
        if (position.Length < paddedCount) VectorUtils.ThrowBufferTooSmall(nameof(position));

        // --- Locals
        // We use BroadcastScalarToVector128 to grab the first TWO floats of each row 
        // and repeat them across the 256-bit register.
        Vector128<float> matrix_row1 = Vector128.Create(matrix.M11, matrix.M12, matrix.M11, matrix.M12);
        Vector128<float> matrix_row2 = Vector128.Create(matrix.M21, matrix.M22, matrix.M21, matrix.M22);
        Vector128<float> matrix_row4 = Vector128.Create(matrix.M41, matrix.M42, matrix.M41, matrix.M42);

        Vector256<float> matrix_0 = Avx.BroadcastVector128ToVector256((float*)&matrix_row1);
        Vector256<float> matrix_1 = Avx.BroadcastVector128ToVector256((float*)&matrix_row2);
        Vector256<float> matrix_3 = Avx.BroadcastVector128ToVector256((float*)&matrix_row4);                    

        fixed (float* position_first = position)
        {
            float* position_ptr = (float*)position_first;

            for (; i < paddedCount; i += 16)
            {
                // --- 1. Load
                Vector256<float> position_0 = Avx.LoadVector256(position_ptr);      // xxxxxxxx Pos2SoA
                Vector256<float> position_1 = Avx.LoadVector256(position_ptr +  8); // yyyyyyyy
                Vector256<float> position_2 = Avx.LoadVector256(position_ptr + 16); // xxxxxxxx
                Vector256<float> position_3 = Avx.LoadVector256(position_ptr + 24); // yyyyyyyy

                // --- 2. Compute
                // position.value = Vector2.Transform(position.value, matrix);
                //   Transform arg[0]
                Vector256<float> temp0_0 = position_0;
                Vector256<float> temp0_1 = position_1;
                Vector256<float> temp0_2 = position_2;
                Vector256<float> temp0_3 = position_3;

                position_0 = AvxVector2.TransformMatrixSoA(temp0_0, temp0_1, Vector256.Create(matrix.M11), Vector256.Create(matrix.M21), Vector256.Create(matrix.M41));
                position_1 = AvxVector2.TransformMatrixSoA(temp0_0, temp0_1, Vector256.Create(matrix.M12), Vector256.Create(matrix.M22), Vector256.Create(matrix.M42));
                position_2 = AvxVector2.TransformMatrixSoA(temp0_2, temp0_3, Vector256.Create(matrix.M11), Vector256.Create(matrix.M21), Vector256.Create(matrix.M41));
                position_3 = AvxVector2.TransformMatrixSoA(temp0_2, temp0_3, Vector256.Create(matrix.M12), Vector256.Create(matrix.M22), Vector256.Create(matrix.M42));

                // --- 3. Store
                Avx.Store(position_ptr,      position_0); // xxxxxxxx
                Avx.Store(position_ptr +  8, position_1); // yyyyyyyy
                Avx.Store(position_ptr + 16, position_2); // xxxxxxxx
                Avx.Store(position_ptr + 24, position_3); // yyyyyyyy

                position_ptr += 32;
            }
        }
        return i;
    }
 
}
