using System;
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

    [GlobalSetup]
    [SetUp]
    public void Setup() {
        store = new EntityStore();
        for (int n = 0; n < EntityCount; n++) {
            store.CreateEntity(
                new FloatComponent  { value = n },
                new FloatComponent2 { value = 1000 + n });
        }
    }
    
    // ---------------------------------------------------------------------------
    [Vectorize(nameof(TunedMoveFloat))][Query]  [OmitHash]
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

        fixed (global::Tests.ECS.FloatComponent* position_first = position)
        fixed (global::Tests.ECS.FloatComponent* velocity_first = velocity)
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
}
