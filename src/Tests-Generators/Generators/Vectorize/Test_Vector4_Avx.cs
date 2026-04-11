// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using Friflo.Vectorization;
using NUnit.Framework;


// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Vectorize;



public static partial class Test_Vector4_Avx
{
    // -----------------------------------------------------------------------------------------------------
    [Vectorize] [OmitHash]
    private static void Multiply([Span] ref Vector3 position, [Span] Vector3 velocity, float deltaTime) {
        position += velocity * deltaTime;
    }

    [Test]
    public static void Test_Avx_Multiply()
    {
        var position        = new Vector3[128];
        var positionVector  = new Vector3[128];
        var velocity        = new Vector3[128];
        for (int n = 0; n < 128; n++) {
            position[n] = positionVector[n] = new  Vector3(n, n + 100, n + 200);
            velocity[n] = new  Vector3(n, n + 10, n + 20);
        }
        MultiplyVector(position,        velocity, 2, false);
        MultiplyVector(positionVector,  velocity, 2);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(position[n], Is.EqualTo(positionVector[n]));
        }
    }
}
