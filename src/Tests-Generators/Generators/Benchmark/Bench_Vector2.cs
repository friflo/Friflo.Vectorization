using System;
using System.Numerics;
using Bench.Lab;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Friflo.Engine.ECS;
using Friflo.Vectorization;
using NUnit.Framework;
using Tests.ECS;
using Tune;

// ReSharper disable InconsistentNaming
namespace Bench;

[BenchmarkCategory("Vector2")]
[MemoryDiagnoser] // Tracks GC allocations
// [Config(typeof(Config))]
public partial class Bench_Vector2
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
    private ArchetypeQuery<Position2,Velocity2> query;
    private ArchetypeQuery<Pos2SoA>             queryPos2SoA;

    const int EntityCount = Constants.EntityCount;
    
    readonly  AlignedArray<Vector2> pos = new (1024);
    readonly  AlignedArray<Vector2> vel = new (1024);
    
    private Matrix3x2   matrix;
    private Matrix4x4   matrix4x4;
    
    [Vectorize][Query]  [OmitHash]
    private static void MultiplyAdd(ref Position2 position, ref Velocity2 velocity, float deltaTime) {
        position.value = velocity.value * deltaTime + position.value;
    }

    [GlobalSetup]
    [SetUp]
    public void Setup() {
        store = new EntityStore();
        for (int n = 0; n < EntityCount; n++) {
            store.CreateEntity(
                new Position2 { value = new Vector2(n,n)},
                new Velocity2 { value = new Vector2(1,2)},
                new FloatComponent { value = n },
                new Pos2SoA { value = new Vector2(n, n + 10_000)});
            pos.Span[n] = new Vector2(n,n);
            vel[n] = new Vector2(1,2);
        }
        query = store.Query<Position2, Velocity2>();
        
        queryPos2SoA = store.Query<Pos2SoA>();
        {
            Matrix3x2 rot = Matrix3x2.CreateRotation(0.5f);
            Matrix3x2 trans = Matrix3x2.CreateTranslation(new Vector2(1f, 2f));
            matrix = Matrix3x2.Multiply(rot, trans);
            for (int n = 0; n < pos.Length; n++) {
                pos[n] = new Vector2(n, n + 1000);
            }
        }
        {
            Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(
                10f * (MathF.PI / 180.0f), // Yaw
                20f * (MathF.PI / 180.0f), // Pitch
                30f * (MathF.PI / 180.0f)  // Roll
            );
            Matrix4x4 trans = Matrix4x4.CreateTranslation(new Vector3(1f, 2f, 3f));
            matrix4x4 = Matrix4x4.Multiply(rot, trans);
        }
    }

    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector2_MultiplyAdd_Query()
    {
        MultiplyAddQuery(store, 0.1f, false);
    }

    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector2_MultiplyAdd_Vectorize()
    {
        MultiplyAddQuery(store, 0.1f);
    }
    
    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector2_MultiplyAdd_ForEachEntity()
    {
        var deltaTime = 0.1f;
        query.ForEachEntity((ref Position2 position, ref Velocity2 velocity, Entity _) => {
            position.value = velocity.value * deltaTime + position.value;
        });
    }
    
    // ------------------------- Vector2_TransformMatrix4x4 --------------------------------------------------
    [Vectorize][Query]  [OmitHash]
    private static void TransformMatrix4x4_AoS(ref Position2 position, Matrix4x4 matrix) {
        position.value = Vector2.Transform(position.value, matrix);
    }
    
    [Benchmark]
    public void Vector2_TransformMatrix4x4_Scalar() {
        TransformMatrix4x4_AoSQuery(store, matrix4x4, false);
    }
    
    [Benchmark] [Test]
    public void Vector2_TransformMatrix4x4_AoS_Vectorized() {
        TransformMatrix4x4_AoSQuery(store, matrix4x4);
    }
    
    [Vectorize][Query]  [OmitHash]
    private static void TransformMatrix4x4_AoSoA(ref Pos2SoA position, Matrix4x4 matrix) {
        position.value = Vector2.Transform(position.value, matrix);
    }
    
    [Benchmark] [Test]
    public void Vector2_TransformMatrix4x4_AoSoA_Vectorized() {
        TransformMatrix4x4_AoSoAQuery(store, matrix4x4);
    }
    
    // ------------------------- Vector2_ECS_MultiplayAdd --------------------------------------------------
    [Vectorize][Query]  [OmitHash]
    private static void ECS_MultiplayAdd_AoS(ref Position2 position, Velocity2 velocity, float deltaTime) {
        position.value += velocity.value * deltaTime;
    }
    
    [Benchmark]  //  dotnet run -c Release --filter *Vector2_ECS_MultiplayAdd*
    public void Vector2_ECS_MultiplayAdd_AoS_Scalar() {
        ECS_MultiplayAdd_AoSQuery(store, 0.1f, false);
    }
    
    [Benchmark] [Test]
    public void Vector2_ECS_MultiplayAdd_AoS_Vectorized() {
        ECS_MultiplayAdd_AoSQuery(store, 0.1f);
    }
    
    [Vectorize][Query]  [OmitHash]
    private static void ECS_MultiplayAdd_AoSoA(ref Pos2SoA position, Pos2SoA velocity, float deltaTime) {
        position.value += velocity.value * deltaTime;
    }
    
    [Benchmark] [Test]
    public void Vector2_ECS_MultiplayAdd_AoSoA_Vectorized() {
        ECS_MultiplayAdd_AoSoAQuery(store, 0.1f);
    }
    
    
    // ------------------------- Vector2_Span_MultiplayAdd --------------------------------------------------
    [Vectorize]  [OmitHash]
    private static void Span_MultiplayAdd_AoS([Span]ref Vector2 position, [Span]Vector2 velocity, float deltaTime) {
        position += velocity * deltaTime;
    }

    [Benchmark]  //  dotnet run -c Release --filter *Vector2_Span_MultiplayAdd*
    public void Vector2_Span_MultiplayAdd_AoS_Scalar() {
        Span_MultiplayAdd_AoSVector(pos.Span, vel.Span, 0.1f, false);
    }
    
    [Benchmark] [Test]
    public void Vector2_Span_MultiplayAdd_AoS_Vectorized() {
        Span_MultiplayAdd_AoSVector(pos.Span, vel.Span, 0.1f);
    }


    
    // ------------------------------------- Lerp -------------------------------------
    [Vectorize][Query]  [OmitHash]
    private static void Vector2Lerp(ref Position2 position, ref Velocity2 velocity, float amount) {
        position.value = Vector2.Lerp(position.value, velocity.value, amount);
    }
    
    [Benchmark]
    public void Vector2_Lerp_Query()
    {
        Vector2LerpQuery(store, 0.1f, false);
    }
    
    [Benchmark]
    public void Vector2_Lerp_Vectorize()
    {
        Vector2LerpQuery(store, 0.1f);
    }
    
    [Benchmark]
    [Test]
    public void Vector2_Transform_Scalar()
    {
        var m = matrix;
        for (int i = 0; i < pos.Length; i++)
        {
            pos[i] = Vector2.Transform(pos[i], m);
        }
    }
    
    [Benchmark]
    [Test]
    public unsafe void Vector2_Transform_AoS()
    {
        fixed(Vector2* vec_ptr = pos.Span)
        {
            Lab_Vector2_TransformAoS.TransformVector2_AoS((float*)vec_ptr, pos.Length, ref matrix);
        }
    }
}
