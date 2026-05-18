// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Numerics;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.GPU;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Kernel;


public partial class Test_Vector2_GPU : GpuTestBase
{
    private readonly Vector2[]    array1  = new Vector2[128];
    private readonly Vector2[]    array2  = new Vector2[128];
    private readonly Vector2[]    buffer1 = new Vector2[128];
    private readonly Vector2[]    buffer2 = new Vector2[128];

    // ----------------------------------------------
    [Kernel] [OmitHash]
    private static void Multiply([Span] ref Vector2 position, [Span] Vector2 velocity) {
        position *= velocity;
    }
        
    [Test]
    public void Test_Kernel_Multiply()
    {
        for (int n = 0; n < 128; n++) {
            array1[n] = buffer1[n] = new Vector2(n,n);
            array2[n] = buffer2[n] = new Vector2(n+100,n+100);
        }
        using var gpuBuffer1   = Device.CreateBuffer(buffer1, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "position");
        using var gpuBuffer2   = Device.CreateBuffer(buffer2, GpuBufferUsage.Storage, "velocity");        

        MultiplyVector(array1,    array2, false);
        MultiplyKernel(gpuBuffer1, gpuBuffer2);
        
        Device.Wait(gpuBuffer1);
        
        gpuBuffer1.Download(gpuBuffer1, buffer1);
        
        for (int n = 0; n < 128; n++) {
            Assert.That(array1[n], Is.EqualTo(buffer1[n]));
        }
    }
}
