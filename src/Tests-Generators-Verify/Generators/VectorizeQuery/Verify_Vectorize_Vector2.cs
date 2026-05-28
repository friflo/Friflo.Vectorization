// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.VectorizeQuery;

public static class Verify_Vectorize_Vector2
{
    private static async Task Verify(string code)
    {
        // 1. Setup (Helper method suggested for readability)
        var compilation = VerifyUtils.CreateCompilation(code);
        var generator = new Gen();
        var driver = CSharpGeneratorDriver.Create(generator);

        // 2. Run
        var runResult = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        
        VerifyUtils.CheckOutputCompilation(outputCompilation);

        // 3. Verify (NUnit adapter)
        await Verifier.Verify(runResult).IgnoreGeneratedResult(VerifyUtils.IgnoreStaticSource);
    }
    
    [Test]
    public static async Task  Verify_Query_MovePosition()
    {
        var code =
"""
using System.Numerics;
using Friflo.Engine.ECS;
using Friflo.Vectorization;

namespace VerifyVectorize;

public struct Position2 : IComponent { public Vector2 value; }
public struct Velocity2 : IComponent { public Vector2 value; }

public partial class MyExample
{
    [Vectorize][Query][OmitHash]
    void MoveExample(ref Position2 position, in Velocity2 velocity) {
        position.value *= velocity.value;
    }
}
""";
        await Verify(code);
    }

    [Test]
    public static async Task  Verify_Query_MovePosition_deltaTime()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }
            public struct Velocity2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void MoveExample(ref Position2 position, in Velocity2 velocity, float deltaTime) {
                    position.value *= velocity.value * deltaTime;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_AssignVector()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }
            public struct Velocity2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position2 position, Vector2 vector) {
                    position.value = vector;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_MultiplyAdd_Assignment()
    {

        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }
            public struct Velocity2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position2 position, in Velocity2 velocity, float deltaTime) {
                    position.value += velocity.value * deltaTime;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_MultiplyAdd()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }
            public struct Velocity2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position2 position, in Velocity2 velocity, float deltaTime) {
                    position.value = velocity.value * deltaTime + position.value;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_scalar_component()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }
            public struct FloatComponent : IComponent { public float value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position2 position, in FloatComponent factor) {
                    position.value = position.value * factor.value;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Set_scalar_component()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }
            public struct FloatComponent : IComponent { public float value; }
            public struct FloatComponent2 : IComponent { public float value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(Position2 position, ref FloatComponent factor, FloatComponent2 factor2) {
                    factor.value = factor2.value;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Set_vector()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(Position2 position, ref Vector2 sum) {
                    sum += position.value;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Min()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position2 position, Vector2 min) {
                    position.value = Vector2.Min(position.value, min);
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Clamp()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position2 position, Vector2 min, Vector2 max) {
                    position.value = Vector2.Clamp(position.value, min, max);
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Lerp()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position2 position, Vector2 vec, float amount) {
                    position.value = Vector2.Lerp(position.value, vec, amount);
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Cross()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2      : IComponent { public Vector2 value; }
            public struct Velocity2      : IComponent { public Vector2 value; }
            public struct FloatComponent : IComponent { public float   value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Cross_Vector2(ref Position2 position, Velocity2 velocity, ref FloatComponent scalar)
                {
                    scalar.value = Vector2.Cross(position.value, velocity.value);
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Transform_Matrix4x4_AoS()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            [AoSoA] public struct Pos2SoA : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Multiply_Matrix4x4(ref Pos2SoA position, Matrix4x4 transform) {
                    position.value = Vector2.Transform(position.value, transform);
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Mixed_Vector2()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position2 : IComponent { public Vector2 value; }
            [AoSoA] public struct Pos2SoA : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Mixed_Vector3(ref Position2 position, Pos2SoA velocity)
                {
                    position.value *= velocity.value;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_NativeSoA_Vector2()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            [AoSoA] public struct Vel2SoA : IComponent { public Vector2 value; }
            [AoSoA] public struct Pos2SoA : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Mixed_Vector3(ref Pos2SoA position, Vel2SoA velocity)
                {
                    position.value *= velocity.value;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_CustomMethod_Vector2()
    {
        var code =
            """
            using System;
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            [AoSoA] public struct Vel2SoA : IComponent { public Vector2 value; }
            [AoSoA] public struct Pos2SoA : IComponent { public Vector2 value; }

            public partial class MyExample
            {
                [Vectorize("MyMethod")][Query]  [OmitHash]
                private static void Mixed_Vector3(ref Pos2SoA position, Vel2SoA velocity)
                {
                    position.value *= velocity.value;
                }
                
                private static unsafe int MyMethod(int count, Span<float> position, Span<float> velocity) { return 0; }
            }
            """;
        await Verify(code);
    }
    
    
    [Test]
    public static async Task  Verify_Span_Vectorize_Multiply()
    {
        var code =
"""
using System.Numerics;
using Friflo.Vectorization;

namespace VerifyVectorize;


public partial class MyExample
{
    [Vectorize]  [OmitHash]
    void MoveExample([Span] ref Vector2 position, [Span] Vector2 velocity, float deltaTime) {
        position += velocity * deltaTime;
    }
}
""";
        await Verify(code);
    }

}
