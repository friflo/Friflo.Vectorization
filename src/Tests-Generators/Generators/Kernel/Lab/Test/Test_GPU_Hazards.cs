using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;


public class Test_GPU_Hazards : KernelBase
{
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
            using var gpuWeight   = device.CreateBuffer(weight, "gpuWeight", BufferProfile.StaticIn);
            using var gpuInput    = device.CreateBuffer(input,  "gpuInput",  BufferProfile.StaticIn);
            using var gpuOutput   = device.CreateBuffer(output, "gpuOutput", BufferProfile.InOut);
            
            var context = device.PipelineContext; 
            context.EnableDiagnostics  = true;
            context.EnablePassBatching = true;
            
            for (int n = 0; n < 5; ++n) {
                GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
            }
            device.Download();
            Assert.AreEqual(3,              context.Records.Length);
            Assert.AreEqual("ShadowMethod", context.Records[0].KernelName);
            Assert.AreEqual("""
                            --- PIPELINE TRACE (Batching: True  Diagnostics: True  Records: 3) ---
                            // Lock-free GPU kernels with deferred, hazard-driven pass batching
                            'ShadowMethod'  calls: 5  passes: 1
                            [KernelSubmit]  'ShadowMethod'
                            [BatchSubmit]
                            """, context.RecordLog);
            
            context.EnablePassBatching = false;
            context.ClearRecords();

            GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
            GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
 
            device.Download();
            
            Assert.AreEqual("""
                            --- PIPELINE TRACE (Batching: False  Diagnostics: True  Records: 5) ---
                            'ShadowMethod'  calls: 1  passes: 1
                            [KernelSubmit]  'ShadowMethod'
                            'ShadowMethod'  calls: 1  passes: 1
                            [KernelSubmit]  'ShadowMethod'
                            [BatchSubmit]
                            """, context.RecordLog);
        }
        Console.WriteLine(HandleDiff.GetState());
    }
    
    [Test]
    public void Test_GPU_Hazard_RAW() // Read-After-Write
    {
        using var device = Device;

        using var weight   = device.CreateBuffer<float>(100, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer<float>(100, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer<float>(100, "output", BufferProfile.InOut);
        
        var context = device.PipelineContext; 
        context.EnableDiagnostics  = true;
        context.EnablePassBatching = true;
        
        GpuPattern.ShadowMethod(weight.In,  input.In, 42,   output.InOut);
        GpuPattern.ShadowMethod(weight.In,  output.In, 42,  input.InOut);
        
        device.Download();
        Assert.AreEqual("""
                        --- PIPELINE TRACE (Batching: True  Diagnostics: True  Records: 6) ---
                        // Lock-free GPU kernels with deferred, hazard-driven pass batching
                        'ShadowMethod'  calls: 1  passes: 1
                        [Pass Split - RAW]
                        [Pass Split - WAR]
                        'ShadowMethod'  calls: 1  passes: 1
                        [KernelSubmit]  'ShadowMethod'
                        [BatchSubmit]
                        """, context.RecordLog);
    }
}