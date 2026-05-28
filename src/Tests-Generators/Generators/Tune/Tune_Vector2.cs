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
    [Vectorize(nameof(StaggeredRhythm))][Query]  [OmitHash]
    private static void TransformMatrix4x4_AoSoA(ref Pos2SoA position, Matrix4x4 matrix) {
        position.value = Vector2.Transform(position.value, matrix);
    }
    
    [Benchmark] [Test] // dotnet run -c Release --filter *Tune_Vector2.Vector2_TransformRhythm*
    public void Vector2_TransformRhythm() {
        TransformMatrix4x4_AoSoAQuery(store, matrix4x4);
    }
    
    // - Broadcast matrix vector at method head
    // - Interleaved Load/Comput/Store blocks (2-block unroll)
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
                // --- STEP 1: All Loads first (Port 2/3) ---
                var x0 = Avx.LoadAlignedVector256(pPtr);        
                var y0 = Avx.LoadAlignedVector256(pPtr + 8);    
                var x1 = Avx.LoadAlignedVector256(pPtr + 16);   
                var y1 = Avx.LoadAlignedVector256(pPtr + 24);   

                // --- STEP 2: Interleaved Math (Port 0/1/5) ---
                // Start X calculations for both blocks to saturate FMAs
                var rx0 = Fma.MultiplyAdd(x0, m11, m41);
                var rx1 = Fma.MultiplyAdd(x1, m11, m41); // Independent of rx0
                
                // Start Y calculations
                var ry0 = Fma.MultiplyAdd(x0, m12, m42);
                var ry1 = Fma.MultiplyAdd(x1, m12, m42); // Independent of ry0

                // Finalize X
                rx0 = Fma.MultiplyAdd(y0, m21, rx0);
                rx1 = Fma.MultiplyAdd(y1, m21, rx1);

                // Finalize Y
                ry0 = Fma.MultiplyAdd(y0, m22, ry0);
                ry1 = Fma.MultiplyAdd(y1, m22, ry1);

                // --- STEP 3: All Stores last (Port 4/7) ---
                Avx.StoreAligned(pPtr, rx0);
                Avx.StoreAligned(pPtr + 8, ry0);
                Avx.StoreAligned(pPtr + 16, rx1);
                Avx.StoreAligned(pPtr + 24, ry1);

                pPtr += 32;
            }
        }
        return i;
    }
    
    // - LSD (Loop Stream Detector) 2-block staggering rhythm
    // - Hiding FMA Latency. 
    // - Port Balancing. Perfect alternating between Port 2/3 (Loads), Port 0/1 (Math), and Port 4/7 (Stores).
    //   Continuous flowing stream of data rather than a "stop-and-go" traffic pattern.
    private static unsafe int StaggeredRhythm(int count, Span<float> position, Matrix4x4 matrix)
    {
        int paddedCount = (count + 15) & ~15;
        int i = 0;
        
        // Setup - same as before
        var m11 = Vector256.Create(matrix.M11);
        var m12 = Vector256.Create(matrix.M12);
        var m21 = Vector256.Create(matrix.M21);
        var m22 = Vector256.Create(matrix.M22);
        var m41 = Vector256.Create(matrix.M41);
        var m42 = Vector256.Create(matrix.M42);

        fixed (float* position_first = position)
        {
            float* pPtr = (float*)position_first;

            // --- The "Staggered 60ns" Candidate ---
            for (; i < paddedCount; i += 16)
            {
                // Block 0: Load
                var x0 = Avx.LoadAlignedVector256(pPtr);        
                var y0 = Avx.LoadAlignedVector256(pPtr + 8);    

                // Block 0: Start Math
                var rx0 = Fma.MultiplyAdd(x0, m11, m41);
                var ry0 = Fma.MultiplyAdd(x0, m12, m42);

                // Block 1: Load (While Block 0 math is in flight)
                var x1 = Avx.LoadAlignedVector256(pPtr + 16);   
                var y1 = Avx.LoadAlignedVector256(pPtr + 24);

                // Block 0: Finish Math
                rx0 = Fma.MultiplyAdd(y0, m21, rx0);
                ry0 = Fma.MultiplyAdd(y0, m22, ry0);

                // Block 1: Start Math
                var rx1 = Fma.MultiplyAdd(x1, m11, m41);
                var ry1 = Fma.MultiplyAdd(x1, m12, m42);

                // Block 0: STORE (Now that ports are free)
                Avx.StoreAligned(pPtr, rx0);
                Avx.StoreAligned(pPtr + 8, ry0);

                // Block 1: Finish Math
                rx1 = Fma.MultiplyAdd(y1, m21, rx1);
                ry1 = Fma.MultiplyAdd(y1, m22, ry1);

                // Block 1: STORE
                Avx.StoreAligned(pPtr + 16, rx1);
                Avx.StoreAligned(pPtr + 24, ry1);

                pPtr += 32;
            }
            // Handle remaining if count isn't multiple of 32 (standard loop)
            // ... (Cleanup loop omitted for brevity)
        }
        return i;
    }
 
}
