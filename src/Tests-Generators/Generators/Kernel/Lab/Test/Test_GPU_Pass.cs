using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;


public class Test_GPU_Pass : KernelBase
{
    [Test]
    public void Test_GPU_Pass_Batching()
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
        context.EnableTraces        = true;
        context.EnablePassBatching  = true;
        _ = context.KernelMetrics;
        
        for (int n = 0; n < 5; ++n) {
            GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        }
        device.Download();
        
        Assert.AreEqual(3,              context.Traces.Length);
        Assert.AreEqual("ShadowMethod", context.Traces[0].KernelName);
        Assert.AreEqual("calls: 5  passes: 1  hazards: 0", context.Stats.ToString());
        Assert.AreEqual("""
                        --- PIPELINE TRACE (Batching: True  Traces: True  Count: 3) ---
                        --- Lock-free GPU kernels with deferred, hazard-driven pass batching
                        'ShadowMethod'  calls: 5  passes: 1
                        [Kernel_Submit]  'ShadowMethod'
                        [Batch_Submit]
                        """, context.TraceLog);
        
        // --- same without traces
        context.EnableTraces  = false;
        context.ClearTraces();
        
        for (int n = 0; n < 5; ++n) {
            GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        }
        device.Download();
        
        Assert.AreEqual(0, context.Traces.Length);
        Assert.AreEqual("calls: 5  passes: 1  hazards: 0", context.Stats.ToString());
        Assert.AreEqual("""
                        --- PIPELINE TRACE (Batching: True  Traces: False  Count: 0) ---
                        --- Lock-free GPU kernels with deferred, hazard-driven pass batching
                        """, context.TraceLog);
        
        // --- Force hazards
        context.EnablePassBatching  = false;
        context.EnableTraces        = true;
        context.ClearTraces();

        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);

        device.Download();
        Assert.AreEqual("calls: 2  passes: 2  hazards: 0", context.Stats.ToString());
        Assert.AreEqual("""
                        --- PIPELINE TRACE (Batching: False  Traces: True  Count: 5) ---
                        'ShadowMethod'  calls: 1  passes: 1
                        [Kernel_Submit]  'ShadowMethod'
                        'ShadowMethod'  calls: 1  passes: 1
                        [Kernel_Submit]  'ShadowMethod'
                        [Batch_Submit]
                        """, context.TraceLog);
    }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Split() // Read-After-Write  &  Write-After-Read
    {
        using var device = Device;

        using var weight   = device.CreateBuffer<float>(100, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer<float>(100, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer<float>(100, "output", BufferProfile.InOut);
        
        var context = device.PipelineContext; 
        context.EnableTraces  = true;
        context.EnablePassBatching = true;
        
        GpuPattern.ShadowMethod(weight.In,  input.In, 42,   output.InOut);
        GpuPattern.ShadowMethod(weight.In,  output.In, 42,  input.InOut);
        
        device.Download();
        Assert.AreEqual("calls: 2  passes: 2  hazards: 2", context.Stats.ToString());
        Assert.AreEqual("""
                        --- PIPELINE TRACE (Batching: True  Traces: True  Count: 6) ---
                        --- Lock-free GPU kernels with deferred, hazard-driven pass batching
                        'ShadowMethod'  calls: 1  passes: 1
                        [Pass_Split - RAW]  Resource: 'output'
                        [Pass_Split - WAR]  Resource: 'input'
                        'ShadowMethod'  calls: 1  passes: 1
                        [Kernel_Submit]  'ShadowMethod'
                        [Batch_Submit]
                        """, context.TraceLog);
        
        // --- same without traces
        context.EnableTraces  = false;
        context.ClearTraces();
        
        GpuPattern.ShadowMethod(weight.In,  input.In, 42,   output.InOut);
        GpuPattern.ShadowMethod(weight.In,  output.In, 42,  input.InOut);
        
        device.Download();
        Assert.AreEqual("calls: 2  passes: 2  hazards: 2", context.Stats.ToString());
        Assert.AreEqual("""
                        --- PIPELINE TRACE (Batching: True  Traces: False  Count: 0) ---
                        --- Lock-free GPU kernels with deferred, hazard-driven pass batching
                        """, context.TraceLog);
    }
}