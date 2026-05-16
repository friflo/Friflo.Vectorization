// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.GPU;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Kernel;


public partial class Test_Float_GPU : GpuTestBase
{
    // -----------------------------------------------------------------------------------------------------
    [Kernel] [OmitHash]
    private static void Multiply([Span] ref float position, [Span] float velocity) {
        position *= velocity;
    }
        
    [Test]
    public void Test_Kernel_Multiply()
    {
        var position    = new float[128];
        var velocity    = new float[128];
        var position2   = new float[128];
        var velocity2   = new float[128];

        for (int n = 0; n < 128; n++) {
            position[n] = position2[n] = n;
            velocity[n] = velocity2[n] = n + 100;
        }
        using var gpuPosition   = Device.CreateBuffer(position2, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuVelocity   = Device.CreateBuffer(velocity2, GpuBufferUsage.Storage, "velocity");        

        MultiplyVector(position,    velocity, false);
        MultiplyKernel(gpuPosition, gpuVelocity);
        
        Device.Wait(gpuPosition);
        
        gpuPosition.Download(gpuPosition, position2);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(position[n], Is.EqualTo(position2[n]));
        }
    }
}
