// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.Vectorization;
using NUnit.Framework;


// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Kernel;



public static partial class Test_Float_GPU
{
    // -----------------------------------------------------------------------------------------------------
    [Kernel] [OmitHash]
    private static void Multiply([Span] ref float position, [Span] float velocity) {
        position *= velocity;
    } 
        
    // [Test]
    public static void Test_Kernel_Multiply()
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
}
