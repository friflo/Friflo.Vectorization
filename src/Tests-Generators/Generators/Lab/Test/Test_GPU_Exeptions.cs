using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public static class Test_GPU_Exeptions
{
    [Test][Ignore("temporary disabled")]
    public static void Test_GPU_Exceptions_GpuBuffer()
    {
        using var context1 = GpuContext.Create();
        using var context2  = GpuContext.Create();
        var weight  = new float[64]; // no alignment
        var input   = new float[64];
        var output  = new float[64];
        for (int n = 0; n < 64; ++n) {
            weight[n] = n;
            input[n]  = n + 1000;
        }
        using var gpuWeight   = new GpuBuffer<float>(context1, weight, BufferUsage.Storage);
        using var gpuInput    = new GpuBuffer<float>(context1, input,  BufferUsage.Storage);
        using var gpuOutput   = new GpuBuffer<float>(context1, output, BufferUsage.Storage | BufferUsage.CopySrc);

        {   // Scope important to Dispose() result 
            using var result = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput);
        }
        {
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput);
            });
            StringAssert.StartsWith("Architectural Blasphemy:", e!.Message!);
        } {

            using var gpuOutput2 = new GpuBuffer<float>(context2, input,  BufferUsage.Storage);
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput2);
            });
            StringAssert.StartsWith("Contextual Polygamy:", e!.Message!);
        } {
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(weight, input, 42, ExeType.GPU, output);
            });
            StringAssert.StartsWith("Identity Crisis:", e!.Message!);
        }
        
    }
    
}