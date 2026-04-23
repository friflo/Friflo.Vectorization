using Bench;
using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Friflo.Vectorization;
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
    [Vectorize][Query]  [OmitHash]
    private static void MoveFloat(ref FloatComponent position, FloatComponent velocity, float deltaTime) {
        position.value += velocity.value * deltaTime;
    }
    
    [Benchmark] [Test] // dotnet run -c Release --filter *Tune_Float.Float_MoveFloat*
    public void Float_MoveFloat() {
        MoveFloatQuery(store, 0.1f);
    }
}
