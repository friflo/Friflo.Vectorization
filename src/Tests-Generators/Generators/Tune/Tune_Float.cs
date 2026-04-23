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
                // --- 1. Load
                Vector256<float> position_0 = Avx.LoadVector256(position_ptr +  0);  // FloatComponent
                Vector256<float> position_1 = Avx.LoadVector256(position_ptr +  8);  // FloatComponent
                Vector256<float> position_2 = Avx.LoadVector256(position_ptr + 16);  // FloatComponent
                Vector256<float> position_3 = Avx.LoadVector256(position_ptr + 24);  // FloatComponent

                Vector256<float> velocity_0 = Avx.LoadVector256(velocity_ptr +  0);  // FloatComponent
                Vector256<float> velocity_1 = Avx.LoadVector256(velocity_ptr +  8);  // FloatComponent
                Vector256<float> velocity_2 = Avx.LoadVector256(velocity_ptr + 16);  // FloatComponent
                Vector256<float> velocity_3 = Avx.LoadVector256(velocity_ptr + 24);  // FloatComponent

                // --- 2. Compute
                // position.value += velocity.value * deltaTime;
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
