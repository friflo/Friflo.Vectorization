using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
public partial class Tune_Float
{
    private EntityStore store;
    const int EntityCount = Constants.EntityCount;
    readonly  AlignedArray positionVec = new AlignedArray(8 * 1024 * 1024);
    readonly  AlignedArray velocityVec = new AlignedArray(8 * 1024 * 1024);

    [GlobalSetup]
    [SetUp]
    public void Setup() {
        store = new EntityStore();
        for (int n = 0; n < EntityCount; n++) {
            store.CreateEntity(
                new FloatComponent  { value = n },
                new FloatComponent2 { value = 1000 + n });
            positionVec.Memory.Span[n] = n;
            velocityVec.Memory.Span[n] = 1000 + n;
        }
    }
    
    // ---------------------------------------------------------------------------
    [Vectorize][Query]  [OmitHash]
    private static void MoveFloat(ref FloatComponent position, FloatComponent velocity, float deltaTime) {
        position.value += velocity.value * deltaTime;
    }
    
    [Benchmark] [Test] // dotnet run -c Release --filter *Tune_Float.Float_MoveFloat*
    public void Float_MoveFloat() {
        MoveFloatQuery(store, 0.1f);
    }
    
    // - "Staggered Memory" Strategy. Interleave the Store of the previous chunk with the Load of the next chunk.
    //   This keeps the Load/Store ports (Ports 2, 3, 4, 7) balanced so the "Write" doesn't block the "Read."
    //   This addresses Store-to-Load Forwarding (SLF) stalls.
    private static unsafe int TunedMoveFloat(int count, Span<FloatComponent> position, ReadOnlySpan<FloatComponent> velocity, float deltaTime)
    {
        int paddedCount = (count + 31) & ~31;
        int i = 0;
        if (position.Length < paddedCount) VectorUtils.ThrowBufferTooSmall(nameof(position));
        if (velocity.Length < paddedCount) VectorUtils.ThrowBufferTooSmall(nameof(velocity));

        // --- Locals
        var deltaTime_scalar = Vector256.Create(deltaTime);

        fixed (FloatComponent* position_first = position)
        fixed (FloatComponent* velocity_first = velocity)
        {
            float* position_ptr = (float*)position_first;
            float* velocity_ptr = (float*)velocity_first;

            for (; i < paddedCount; i += 32)
            {
                // --- Chunk 0: Load & Math
                var pos0 = Avx.LoadVector256(position_ptr + 0);
                var vel0 = Avx.LoadVector256(velocity_ptr + 0);
                var res0 = Fma.MultiplyAdd(vel0, deltaTime_scalar, pos0);

                // --- Chunk 1: Load & Math
                var pos1 = Avx.LoadVector256(position_ptr + 8);
                var vel1 = Avx.LoadVector256(velocity_ptr + 8);
                var res1 = Fma.MultiplyAdd(vel1, deltaTime_scalar, pos1);

                // --- Chunk 0: STORE (While Chunk 2 is loading)
                Avx.Store(position_ptr + 0, res0);

                // --- Chunk 2: Load & Math
                var pos2 = Avx.LoadVector256(position_ptr + 16);
                var vel2 = Avx.LoadVector256(velocity_ptr + 16);
                var res2 = Fma.MultiplyAdd(vel2, deltaTime_scalar, pos2);

                // --- Chunk 1: STORE
                Avx.Store(position_ptr + 8, res1);

                // --- Chunk 3: Load & Math
                var pos3 = Avx.LoadVector256(position_ptr + 24);
                var vel3 = Avx.LoadVector256(velocity_ptr + 24);
                var res3 = Fma.MultiplyAdd(vel3, deltaTime_scalar, pos3);

                // --- Chunk 2 & 3: Final STORES
                Avx.Store(position_ptr + 16, res2);
                Avx.Store(position_ptr + 24, res3);

                position_ptr += 32;
                velocity_ptr += 32;
            }
        }
        return i;
    }
    
    // ---------------------------------------------------------------------------
    [Vectorize(nameof(MoveFloatVec_Tune))] [OmitHash]
    private static void MoveFloatVec([Span] ref float position, [Span] float velocity, float deltaTime) {
        position += velocity * deltaTime;
    }
    
    [Benchmark] [Test]   //     dotnet run -c Release --filter *Tune_Float.Float_MoveFloatVec*
    public void Float_MoveFloatVector_Scalar() {
        MoveFloatVecVector(positionVec.Span, velocityVec.Span, 0.1f, false);
    }
    
    [Benchmark] [Test]   //     dotnet run -c Release --filter *Tune_Float.Float_MoveFloatVec*
    public void Float_MoveFloatVector_Vectorize() {
        MoveFloatVecVector(positionVec.Span, velocityVec.Span, 0.1f);
    }
    
    
    // mkl_avx2.3.dll   mkl_rt.dll  mkl_rt.3.dll
    [DllImport("mkl_rt.3.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern unsafe void cblas_saxpy(
        int n,           // Number of elements
        float a,         // Scalar multiplier (deltaTime)
        float* x,        // Source array (velocity)
        int incx,        // Stride for x (usually 1)
        float* y,        // Destination array (position)
        int incy         // Stride for y (usually 1)
    );
    
    [Benchmark] [Test]   //     dotnet run -c Release --filter *Tune_Float.Float_MoveFloatVec*
    public unsafe void Float_MoveFloatVector_IntelMKL() {
        
        fixed (float* position_ptr = positionVec.Span)
        fixed (float* velocity_ptr = velocityVec.Span) {
            cblas_saxpy(positionVec.Span.Length, 0.1f, velocity_ptr, 1, position_ptr, 1);
        }
    }
    
    [SkipLocalsInit]
    private static unsafe int MoveFloatVec_Tune(int count,
        Span<float> position,
        ReadOnlySpan<float> velocity,
        float deltaTime)
    {
        int i = 0;
        count -= 32;
        if (i > count) {
            return 0;
        }
        if (position.Length < count) VectorUtils.ThrowBufferTooSmall(nameof(position));
        if (velocity.Length < count) VectorUtils.ThrowBufferTooSmall(nameof(velocity));

        // --- Locals
        var deltaTime_scalar = Vector256.Create(deltaTime);

        fixed (float* position_first = position)
        fixed (float* velocity_first = velocity)
        {
            float* position_ptr = (float*)position_first;
            float* velocity_ptr = (float*)velocity_first;
            const int PrefetchDistance = 64;

            for (; i <= count; i += 32)
            {
                Sse.Prefetch0(position_ptr + PrefetchDistance);
                Sse.Prefetch0(velocity_ptr + PrefetchDistance);
                
                // --- 1. Load
                Vector256<float> position_0 = Avx.LoadVector256(position_ptr +  0);  // Single
                Vector256<float> position_1 = Avx.LoadVector256(position_ptr +  8);  // Single
                Vector256<float> position_2 = Avx.LoadVector256(position_ptr + 16);  // Single
                Vector256<float> position_3 = Avx.LoadVector256(position_ptr + 24);  // Single

                Vector256<float> velocity_0 = Avx.LoadVector256(velocity_ptr +  0);  // Single
                Vector256<float> velocity_1 = Avx.LoadVector256(velocity_ptr +  8);  // Single
                Vector256<float> velocity_2 = Avx.LoadVector256(velocity_ptr + 16);  // Single
                Vector256<float> velocity_3 = Avx.LoadVector256(velocity_ptr + 24);  // Single

                // --- 2. Compute
                // position += velocity * deltaTime;
                position_0 = Fma.MultiplyAdd(velocity_0, deltaTime_scalar, position_0);
                position_1 = Fma.MultiplyAdd(velocity_1, deltaTime_scalar, position_1);
                position_2 = Fma.MultiplyAdd(velocity_2, deltaTime_scalar, position_2);
                position_3 = Fma.MultiplyAdd(velocity_3, deltaTime_scalar, position_3);

                // --- 3. Store
                Avx.Store(position_ptr +  0, position_0);
                Avx.Store(position_ptr +  8, position_1);
                Avx.Store(position_ptr + 16, position_2);
                Avx.Store(position_ptr + 24, position_3);

                position_ptr += 32;
                velocity_ptr += 32;
            }
        }
        return i;
    }

}
