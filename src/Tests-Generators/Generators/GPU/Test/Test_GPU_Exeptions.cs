using System;
using System.Diagnostics;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using NUnit.Framework.Legacy;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.GPU;

public class Test_GPU_Exeptions : GpuTestBase
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
        using var gpuWeight   = new GpuBuffer<float>(device1, weight, GpuBufferUsage.Storage, "gpuWeight");
        using var gpuInput    = new GpuBuffer<float>(device1, input,  GpuBufferUsage.Storage, "gpuInput");
        using var gpuOutput   = new GpuBuffer<float>(device1, output, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "gpuOutput");
        
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
            using var gpuOutput2 = new GpuBuffer<float>(device2, input,  GpuBufferUsage.Storage, "gpuOutput2");
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput2);
            });
            StringAssert.StartsWith("Diplomatic Incident:", e!.Message!);
        } {
            using var gpuOutputSmall = new GpuBuffer<float>(device1, new float[63],  GpuBufferUsage.Storage, "gpuOutput1");
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutputSmall);
            });
            StringAssert.StartsWith("Totalitarian Sizing:", e!.Message!);
        } {
            using var gpuOutput1 = new GpuBuffer<float>(device1, input,  GpuBufferUsage.Storage, "gpuOutput1");
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
            using var gpuWeight   = new GpuBuffer<float>(device, weight, GpuBufferUsage.Storage, "gpuWeight");
            using var gpuInput    = new GpuBuffer<float>(device, input,  GpuBufferUsage.Storage, "gpuInput");
            using var gpuOutput   = new GpuBuffer<float>(device, output, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "gpuOutput");
            
            int count = 0;
            
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