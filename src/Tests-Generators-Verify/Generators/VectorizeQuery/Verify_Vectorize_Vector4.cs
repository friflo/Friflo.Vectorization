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

public static class Verify_Vectorize_Vector4
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

public struct Position4 : IComponent { public Vector4 value; }
public struct Velocity4 : IComponent { public Vector4 value; }

public partial class MyExample
{
    [Vectorize][Query][OmitHash]
    void MoveExample(ref Position4 position, in Velocity4 velocity) {
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

            public struct Position4 : IComponent { public Vector4 value; }
            public struct Velocity4 : IComponent { public Vector4 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void MoveExample(ref Position4 position, in Velocity4 velocity, float deltaTime) {
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

            public struct Position4 : IComponent { public Vector4 value; }
            public struct Velocity4 : IComponent { public Vector4 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position4 position, Velocity4 vector) {
                    position.value = vector.value;
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

            public struct Position4 : IComponent { public Vector4 value; }
            public struct Velocity4 : IComponent { public Vector4 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position4 position, in Velocity4 velocity, float deltaTime) {
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

            public struct Position4 : IComponent { public Vector4 value; }
            public struct Velocity4 : IComponent { public Vector4 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position4 position, in Velocity4 velocity, float deltaTime) {
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

            public struct Position4 : IComponent { public Vector4 value; }
            public struct FloatComponent : IComponent { public float value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position4 position, in FloatComponent factor) {
                    position.value = position.value * factor.value;
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Query_Multiply_Matrix4x4()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position4 : IComponent { public Vector4 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Multiply_Matrix4x4(ref Position4 position, Matrix4x4 transform) {
                    position.value = Vector4.Transform(position.value, transform);
                }
            }
            """;
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Mixed_Vector4()
    {
        var code =
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position4 : IComponent { public Vector4 value; }
            [AoSoA] public struct Pos4SoA : IComponent { public Vector4 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Mixed_Vector4(ref Position4 position, Pos4SoA velocity)
                {
                    position.value *= velocity.value;
                }
            }
            """;
        await Verify(code);
    }
}
