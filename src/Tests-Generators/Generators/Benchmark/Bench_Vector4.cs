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

[BenchmarkCategory("Vector4")]
[MemoryDiagnoser] // Tracks GC allocations
// [Config(typeof(Config))]
public partial class Bench_Vector4
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
    private ArchetypeQuery<Position4,Velocity4> query;
    private Matrix4x4 matrix;
    private AlignedArray<Vector4> pos   = new (Constants.VectorCount);
    private AlignedArray<Vector4> vel   = new (Constants.VectorCount);
    private AlignedArray<Vector4> result = new (Constants.VectorCount);


    const int EntityCount = Constants.EntityCount;
    
    [Vectorize][Query]  [OmitHash]
    private static void MultiplyAdd(ref Position4 position, ref Velocity4 velocity, float deltaTime) {
        position.value = velocity.value * deltaTime + position.value;
    }

    [GlobalSetup]
    [SetUp]
    public void Setup() {
        store = new EntityStore();
        for (int n = 0; n < EntityCount; n++) {
            store.CreateEntity(
                new Position4 { value = new Vector4(n,n,n,n)},
                new Velocity4 { value = new Vector4(1,2,3,4)},
                new FloatComponent { value = n },
                new Pos4SoA() { value = new Vector4(n,n,n,n) });
        }
        query = store.Query<Position4, Velocity4>();
        Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(
            10f * (MathF.PI / 180.0f), // Yaw
            20f * (MathF.PI / 180.0f), // Pitch
            30f * (MathF.PI / 180.0f)  // Roll
        );
        Matrix4x4 trans = Matrix4x4.CreateTranslation(new Vector3(1f, 2f, 3f));
        matrix = Matrix4x4.Multiply(rot, trans);
        for (int n = 0; n < pos.Length; n++) {
            pos[n] = new Vector4(n, n + 1000, n + 2000, n + 3000);
            vel[n] = new Vector4(n, n * 2 , n * 3, n * 4);
        }
    }

    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector4_MultiplyAdd_Query()
    {
        MultiplyAddQuery(store, 0.1f, false);
    }

    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector4_MultiplyAdd_Vectorize()
    {
        MultiplyAddQuery(store, 0.1f);
    }
    
    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector4_MultiplyAdd_ForEachEntity()
    {
        var deltaTime = 0.1f;
        query.ForEachEntity((ref Position4 position, ref Velocity4 velocity, Entity _) => {
            position.value = velocity.value * deltaTime + position.value;
        });
    }

    // ---------------------------------------------------------------------------
    [Vectorize][Query]  [OmitHash]
    private static void TransformMatrix4x4_AoS(ref Position4 position, Matrix4x4 matrix) {
        position.value = Vector4.Transform(position.value, matrix);
    }
    
    [Benchmark]
    public void Vector4_TransformMatrix4x4()
    {
        TransformMatrix4x4_AoSQuery(store, matrix, false);
    }
    
    [Benchmark] [Test]
    public void Vector4_TransformMatrix4x4_AoS_Vectorized()
    {
        var query = TransformMatrix4x4_AoSQuery(store, matrix);
    }
    
    [Vectorize][Query]  [OmitHash]
    private static void TransformMatrix4x4_AoSoA(ref Pos4SoA position, Matrix4x4 matrix) {
        position.value = Vector4.Transform(position.value, matrix);
    }
    
    [Benchmark] [Test]
    public void Vector4_TransformMatrix4x4_AoSoA_Vectorized()
    {
        var query = TransformMatrix4x4_AoSoAQuery(store, matrix);
    }
    
    // ------------------------- Vector4_Span_MultiplayAdd --------------------------------------------------
    [Vectorize]  [OmitHash]
    private static void Span_MultiplayAdd_AoS([Span]ref Vector4 position, [Span]Vector4 velocity, float deltaTime) {
        position += velocity * deltaTime;
    }

    [Benchmark]  //  dotnet run -c Release --filter *Vector4_Span_MultiplayAdd*
    public void Vector4_Span_MultiplayAdd_AoS_Scalar() {
        Span_MultiplayAdd_AoSVector(pos.Span, vel.Span, 0.1f, false);
    }
    
    [Benchmark] [Test]
    public void Vector4_Span_MultiplayAdd_AoS_Vectorized() {
        Span_MultiplayAdd_AoSVector(pos.Span, vel.Span, 0.1f);
    }
    
    [Vectorize] [Query]  [OmitHash]
    private static void ECS_MultiplayAdd_AoSoA(ref Pos4SoA position, Pos4SoA velocity, float deltaTime) {
        position.value += velocity.value * deltaTime;
    }
    
    [Benchmark] [Test]
    public void Vector4_ECS_MultiplayAdd_AoSoA_Vectorized() {
        ECS_MultiplayAdd_AoSoAQuery(store, 0.1f);
    }
    
    [Benchmark] [Test]   //     dotnet run -c Release --filter *Vector2_Span_MultiplayAdd_AoS_IntelMKL*
    public unsafe void Vector4_Span_MultiplayAdd_AoS_IntelMKL() {

        fixed (Vector4* pPtr = pos.Span)
        fixed (Vector4* vPtr = vel.Span)
        {
            float* pFloat = (float*)pPtr;
            float* vFloat = (float*)vPtr;
            Bench_Vector2.cblas_saxpy(4 * 1024, 0.1f, vFloat, 1, pFloat, 1);
        }
    }
    
    // ------------------------------------- Lerp -------------------------------------
    [Vectorize][Query]  [OmitHash]
    private static void Vector4Lerp(ref Position4 position, ref Velocity4 velocity, float amount) {
        position.value = Vector4.Lerp(position.value, velocity.value, amount);
    }
    
    [Benchmark]
    public void Vector4_Lerp_Query()
    {
        Vector4LerpQuery(store, 0.1f, false);
    }
    
    [Benchmark]
    public void Vector4_Lerp_Vectorize()
    {
        Vector4LerpQuery(store, 0.1f);
    }
    
    // ------------------------------------- Cross -------------------------------------
    [Vectorize] [OmitHash]
    private static void Vector4Cross([Span] ref Vector4 result, [Span] Vector4 vec1, [Span] Vector4 vec2) {
        result = Vector4.Cross(vec1, vec2);
    }
    
    [Benchmark]
    public void Vector4_Cross_for()
    {
        for (int n = 0; n < pos.Length; n++) {
            result[n] = Vector4.Cross(pos[n], vel[n]);
        }
    }
    
    [Benchmark]
    [Test]
    public unsafe void Vector4_Cross_Lab()
    {
        fixed(Vector4* vec1_ptr = pos.Span)
        fixed(Vector4* vec2_ptr = vel.Span)
        fixed(Vector4* res_ptr  = result.Span)
        {
            for (int n = 0; n < pos.Length; n += 8) {
                // Lab_Vector4_Cross.ComputeCrossProduct8(vec1_ptr + n, vec2_ptr + n, res_ptr + n);
                Lab_Vector4_Cross_2.ComputeCrossProduct8_NoScatter(vec1_ptr + n, vec2_ptr + n, res_ptr + n);
            }
        }
    }
}
