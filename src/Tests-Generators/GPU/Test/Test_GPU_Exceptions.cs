using System;
using System.Diagnostics;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using NUnit.Framework.Legacy;

// ReSharper disable InconsistentNaming
namespace Tests.GPU;

public class Test_GPU_Exceptions : GpuTestBase
{
    [Test]
    public void Test_GPU_Exceptions_GpuBuffer()
    {
        Assert.IsFalse(Instance.IsDisposed);
        Assert.IsFalse(Adapter.IsDisposed);
        
        using var device1   = Adapter.CreateDevice("device1");
        using var device2   = Adapter.CreateDevice("device2");
        Assert.AreEqual("device1: Alive", device1.ToString());
        
        var weight  = new float[64]; // no alignment
        var input   = new float[64];
        var output  = new float[64];
        for (int n = 0; n < 64; ++n) {
            weight[n] = n;
            input[n]  = n + 1000;
        }
        using var gpuWeight   = device1.CreateBuffer(weight, GpuBufferUsage.Storage, "gpuWeight");
        using var gpuInput    = device1.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuInput");
        using var gpuOutput   = device1.CreateBuffer(output, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "gpuOutput");
        
        StringAssert.StartsWith("gpuWeight(", gpuWeight.ToString());
        StringAssert.EndsWith  ("): Alive",   gpuWeight.ToString());
        Assert.IsFalse(gpuWeight.IsDisposed);

        {   // Scope important to Dispose() result (=output)
            using var result = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput);
        } {
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput);
            });
            StringAssert.StartsWith("Existential Void:", e!.Message!);
        } {
            using var gpuOutput2 = device2.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuOutput2");
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput2);
            });
            StringAssert.StartsWith("Diplomatic Incident:", e!.Message!);
        } {
            using var gpuOutputSmall = device1.CreateBuffer(new float[63],  GpuBufferUsage.Storage, "gpuOutput1");
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutputSmall);
            });
            StringAssert.StartsWith("Totalitarian Sizing:", e!.Message!);
        } {
            using var gpuOutput1 = device1.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuOutput1");
            device1.Dispose();
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput1);
            });
            StringAssert.StartsWith("Archaeological Error:", e!.Message!);
        }
        {
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(weight, input, 42, ExeType.GPU, output);
            });
            StringAssert.StartsWith("Identity Crisis:", e!.Message!);
        }
        
    }
    
    [Test]
    public void Test_GPU_Repeat()
    {
        {
            using var device = Device;

            var weight  = new float[64]; // no alignment
            var input   = new float[64];
            var output  = new float[64];
            for (int n = 0; n < 64; ++n) {
                weight[n] = n;
                input[n]  = n + 1000;
            }
            using var gpuWeight   = device.CreateBuffer(weight, GpuBufferUsage.Storage, "gpuWeight");
            using var gpuInput    = device.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuInput");
            using var gpuOutput   = device.CreateBuffer(output, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "gpuOutput");
            
            GpuBuffer<float> result = null;
            for (int n = 0; n < 5; ++n) {
                result = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput);
                Debug.WriteLine(Handles.GetState());
            }
            device.Wait(result);
        }
        Debug.WriteLine(Handles.GetState());
    }

    
}