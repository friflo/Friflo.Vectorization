using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Bench.Lab;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Friflo.Engine.ECS;
using Friflo.Vectorization;
using NUnit.Framework;
using Tests.ECS;

// ReSharper disable InconsistentNaming
namespace Bench;


[BenchmarkCategory("Vector3")]
[MemoryDiagnoser] // Tracks GC allocations
// [Config(typeof(Config))]
public partial class Bench_Vector3
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
    private ArchetypeQuery<Position,Velocity>   query;
    private ArchetypeQuery<Pos3SoA>             queryPos3SoA;

    const int EntityCount = Constants.EntityCount;
    
    private Vector3[]   vec1    = new Vector3[Constants.VectorCount];
    private float[]     vec1_x  = new float[Constants.VectorCount];
    private float[]     vec1_y  = new float[Constants.VectorCount];
    private float[]     vec1_z  = new float[Constants.VectorCount];
    private Matrix4x4   matrix;
    
    
    [Vectorize][Query]  [OmitHash]
    private static void MultiplyAdd(ref Position position, ref Velocity velocity, float deltaTime) {
        position.value = velocity.value * deltaTime + position.value;
    }

    [GlobalSetup]
    [SetUp]
    public void Setup() {
        store = new EntityStore();
        for (int n = 0; n < EntityCount; n++) {
            store.CreateEntity(
                new Position(n,n,n),
                new Velocity { value = new Vector3(1,2,3)},
                new FloatComponent { value = n },
                new Pos3SoA { value = new Vector3(n, n + 10_000,  n + 20_000)}
                );
        }
        query = store.Query<Position, Velocity>();
        queryPos3SoA = store.Query<Pos3SoA>();
        
        Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(
            10f * (MathF.PI / 180.0f), // Yaw
            20f * (MathF.PI / 180.0f), // Pitch
            30f * (MathF.PI / 180.0f)  // Roll
        );
        Matrix4x4 trans = Matrix4x4.CreateTranslation(new Vector3(1f, 2f, 3f));
        matrix = Matrix4x4.Multiply(rot, trans);
        for (int n = 0; n < vec1.Length; n++) {
            vec1[n] = new Vector3(n, n + 1000, n + 2000);
            vec1_x[n] = vec1[n].X;
            vec1_y[n] = vec1[n].Y;
            vec1_z[n] = vec1[n].Z;
        }
    }

    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector3_MultiplyAdd_Query()
    {
        MultiplyAddQuery(store, 0.1f, false);
    }

    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector3_MultiplyAdd_Vectorize()
    {
        MultiplyAddQuery(store, 0.1f);
    }
    
    [Benchmark]
    [Comment("pos.value = vel.value * dt + pos.value;")]
    public void Vector3_MultiplyAdd_ForEachEntity()
    {
        var deltaTime = 0.1f;
        query.ForEachEntity((ref Position position, ref Velocity velocity, Entity _) => {
            position.value = velocity.value * deltaTime + position.value;
        });
    }
    
    // ------------------------------------- Lerp -------------------------------------
    [Vectorize][Query]  [OmitHash]
    private static void Vector3Lerp(ref Position position, ref Velocity velocity, float amount) {
        position.value = Vector3.Lerp(position.value, velocity.value, amount);
    }
    
    [Benchmark]
    public void Vector3_Lerp_Query()
    {
        Vector3LerpQuery(store, 0.1f, false);
    }
    
    [Benchmark]
    public void Vector3_Lerp_Vectorize()
    {
        Vector3LerpQuery(store, 0.1f);
    }
    
    // ------------------------------------- Transform ------------------------------
    [Benchmark]
    [Test]
    public unsafe void Vector3_Transform_SoA()
    {
        fixed(float* vec1_x_ptr = vec1_x)
        fixed(float* vec1_y_ptr = vec1_y)
        fixed(float* vec1_z_ptr = vec1_z) {
            Lab_Vector3_TransformSoA.TransformSoA(vec1_x_ptr,vec1_y_ptr,vec1_z_ptr, vec1_x.Length, matrix);
        }
    }
    
    [Benchmark]
    [Test]
    public unsafe void Vector3_Transform_AoS()
    {
        fixed(Vector3* vec_ptr = vec1)
        {
            Lab_Vector3_TransformAoS.TransformAoS(vec_ptr, vec1.Length, matrix);
        }
    }
    
    [Benchmark]
    [Test]
    public unsafe void Vector3_Transform_ECS_SoA()
    {
        foreach (var (pos3SoA, entities) in queryPos3SoA.Chunks)
        {
            var lanes = pos3SoA.GetLanesSoA();
            fixed(float* vec_ptr = lanes)
            {
                // if (logLanePtr) { LogLanePtr(vec_ptr); logLanePtr = false; }
                var stride = pos3SoA.GetStrideSoA();
                Lab_Vector3_TransformEcsSoA.TransformSoA(vec_ptr, entities.Length, stride, matrix);    
            }
        }
    }
    
    private bool logLanePtr = true;
    
    private  static unsafe void LogLanePtr(float* ptr)
    {
        long address = (long)ptr;
        bool isAligned = (address % 32) == 0;
        Console.WriteLine($"Base Address: {address:X} - 32-Byte Aligned: {isAligned}");
    }

    

    
}
