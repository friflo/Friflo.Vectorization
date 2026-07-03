// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.VectorizeQuery;

public static class Verify_Vectorize_Vector3
{
    private static async Task Verify([LanguageInjection("csharp")] string code)
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
        await Verify(
"""
using System.Numerics;
using Friflo.Engine.ECS;
using Friflo.Vectorization;

namespace VerifyVectorize;

public struct Velocity : IComponent { public Vector3 value; }

public partial class MyExample
{
    [Vectorize][Query][OmitHash]
    void MoveExample(ref Position position, in Velocity velocity) {
        position.value *= velocity.value;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Query_MovePosition_deltaTime()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Velocity : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void MoveExample(ref Position position, in Velocity velocity, float deltaTime) {
                    position.value *= velocity.value * deltaTime;
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_AssignVector()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Velocity : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position position, Vector3 vector) {
                    position.value = vector;
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_MultiplyAdd_Assignment()
    {

        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Velocity : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position position, in Velocity velocity, float deltaTime) {
                    position.value += velocity.value * deltaTime;
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_MultiplyAdd()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Velocity : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position position, in Velocity velocity, float deltaTime) {
                    position.value = velocity.value * deltaTime + position.value;
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_scalar_component()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct FloatComponent : IComponent { public float value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void AssignVector(ref Position position, in FloatComponent factor) {
                    position.value = position.value * factor.value;
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_Lerp()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Lerp(ref Position3 position, Vector3 vec, Vector3 amount) {
                    position.value = Vector3.Lerp(position.value, vec, amount);
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_static()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Access_static(ref Position3 position) {
                    position.value = Vector3.Pi;
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_Truncate()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Access_static(ref Position3 position) {
                    position.value = Vector3.Truncate(position.value);
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_Cross()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }
            public struct Velocity3 : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Cross(ref Position3 position, Velocity3 velocity) {
                    position.value = Vector3.Cross(position.value, velocity.value);
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_Normalize()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }
            public struct Velocity3 : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Normalize(ref Position3 position, Velocity3 velocity) {
                    position.value = Vector3.Normalize(velocity.value);
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_Length()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3      : IComponent { public Vector3 value; }
            public struct FloatComponent : IComponent { public float   value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Length_Vector3(Position3 position, ref FloatComponent length)
                {
                    length.value = position.value.Length();
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_Distance()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }
            public struct Velocity3 : IComponent { public Vector3 value; }
            public struct Distance  : IComponent { public float   value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Distance_Vector3(Position3 position, Position3 velocity, ref Distance distance)
                {
                    distance.value = Vector3.Distance(position.value, velocity.value);
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Query_Transform_Matrix4x4_AoS()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                void Multiply_Matrix4x4(ref Position3 position, Matrix4x4 transform) {
                    position.value = Vector3.Transform(position.value, transform);
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_Mixed_Vector3()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            public struct Position3 : IComponent { public Vector3 value; }
            [AoSoA] public struct Pos3SoA : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Mixed_Vector3(ref Position3 position, Pos3SoA velocity)
                {
                    position.value *= velocity.value;
                }
            }
            """);
    }
    
    [Test]
    public static async Task  Verify_NativeSoA_Vector3()
    {
        await Verify(
            """
            using System.Numerics;
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyVectorize;

            [AoSoA] public struct Pos3SoA : IComponent { public Vector3 value; }
            [AoSoA] public struct Vel3SoA : IComponent { public Vector3 value; }

            public partial class MyExample
            {
                [Vectorize][Query]  [OmitHash]
                private static void Mixed_Vector3(ref Pos3SoA position, Vel3SoA velocity)
                {
                    position.value *= velocity.value;
                }
            }
            """);
    }
}


