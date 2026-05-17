// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.Vectorization;
using NUnit.Framework;


// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Vectorize;



public static partial class Test_Float_Avx
{
    // -----------------------------------------------------------------------------------------------------
    [Vectorize] [OmitHash]
    private static void Multiply([Span] ref float position, [Span] float velocity) {
        position *= velocity;
    } 
        
    [Test]
    public static void Test_Avx_Multiply()
    {
        var position        = new float[128];
        var positionVector  = new float[128];
        var velocity        = new float[128];
        for (int n = 0; n < 128; n++) {
            position[n] = positionVector[n] = n;
            velocity[n] = n + 100;
        }
        MultiplyVector(position,        velocity, false);
        MultiplyVector(positionVector,  velocity);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(position[n], Is.EqualTo(positionVector[n]));
        }
    }
    
    // ----------------------------------------------
    [Vectorize] [OmitHash]
    private static void Avx_Trigonometry2([Span]ref float position)
    {
        var sinh     = MathF.Sinh(position);
        var cosh     = MathF.Cosh(position);
        var tanh     = MathF.Tanh(position);
        position += sinh + cosh + tanh;
    }
    
    [Test]
    public static void Test_Avx_Trigonometry2()
    {
        var scalar1     = new float[128];
        var scalar2     = new float[128];
        for (int n = 0; n < 128; n++) {
            scalar1[n] = scalar2[n] = (n - 64f) / 64f * 10;
        }

        Avx_Trigonometry2Vector(scalar1, false);
        Avx_Trigonometry2Vector(scalar2);
        
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(scalar2[n]).Within(0.01).Percent);
            Assert.That(scalar1[n], Is.Not.NaN & Is.Not.EqualTo(float.PositiveInfinity) & Is.Not.EqualTo(float.NegativeInfinity));
        }
    }
}
