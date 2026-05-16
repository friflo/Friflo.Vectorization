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
    private readonly float[]    scalar1 = new float[128];
    private readonly float[]    scalar2 = new float[128];
    private readonly float[]    buffer1 = new float[128];
    private readonly float[]    buffer2 = new float[128];
    
    // -----------------------------------------------------------------------------------------------------
    [Kernel] [OmitHash]
    private static void Multiply([Span] ref float position, [Span] float velocity) {
        position *= velocity;
    }
        
    [Test]
    public void Test_Kernel_Multiply()
    {
        for (int n = 0; n < 128; n++) {
            scalar1[n] = buffer1[n] = n;
            scalar2[n] = buffer2[n] = n + 100;
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        MultiplyVector(scalar1,    scalar2, false);
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(scalar1[n], Is.EqualTo(buffer1[n]));
        }
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
    }
}
