using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;


public partial class Test_GPU_Pass : KernelBase
{
    [Test]
    public void Test_GPU_Pass_Batching()
    {
        using var device = Device;

        var weight  = new float[64];
        var input   = new float[64];
        var output  = new float[64];
        for (int n = 0; n < 64; ++n) {
            weight[n] = n;
            input[n]  = n + 1000;
        }
        using var gpuWeight   = device.CreateBuffer(weight, "gpuWeight", BufferProfile.StaticIn);
        using var gpuInput    = device.CreateBuffer(input,  "gpuInput",  BufferProfile.StaticIn);
        using var gpuOutput   = device.CreateBuffer(output, "gpuOutput", BufferProfile.InOut);
        
        device.EnableTraces        = true;
        device.EnablePassBatching  = true;
        _ = device.KernelMetrics;
        
        for (int n = 0; n < 5; ++n) {
            Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        }
        device.Download();
        
        Assert.AreEqual(3,              device.Traces.Length);
        Assert.AreEqual("MultiplyAddKernel", device.Traces[0].KernelName);
        Assert.AreEqual("calls: 5  passes: 1  hazards: 0", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 5  passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  5   new_pass
            > Kernel_Submit
            > Batch_Submit
            """).IgnoreWhiteSpace);
        
        // --- same without traces
        device.EnableTraces  = false;
        device.ClearTraces();
        
        for (int n = 0; n < 5; ++n) {
            Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        }
        device.Download();
        
        Assert.AreEqual(0, device.Traces.Length);
        Assert.AreEqual("calls: 5  passes: 1  hazards: 0", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 5  passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            """).IgnoreWhiteSpace);
        
        // --- Force hazards
        device.EnablePassBatching  = false;
        device.EnableTraces        = true;
        device.ClearTraces();

        Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);

        device.Download();
        Assert.AreEqual("calls: 2  passes: 2  hazards: 0", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: False  calls: 2  passes: 2  hazards: 0) ---
            MultiplyAddKernel()             calls:  1   new_pass
            > Kernel_Submit
            MultiplyAddKernel()             calls:  1   new_pass
            > Kernel_Submit
            > Batch_Submit
            """).IgnoreWhiteSpace);
    }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Split() // Read-After-Write  &  Write-After-Read
    {
        using var device = Device;

        using var weight   = device.CreateBuffer<float>(100, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer<float>(100, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer<float>(100, "output", BufferProfile.InOut);
        
        device.EnableTraces  = true;
        device.EnablePassBatching = true;
        
        Pattern.MultiplyAddKernel(weight.In,  input.In, 42,   output.InOut);
        Pattern.MultiplyAddKernel(weight.In,  output.In, 42,  input.InOut);
        
        device.Download();
        Assert.AreEqual("calls: 2  passes: 2  hazards: 2", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 2  passes: 2  hazards: 2) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  1   new_pass
              | RAW 'output'
              | WAR 'input'
            MultiplyAddKernel()             calls:  1   pass_split
            > Kernel_Submit
            > Batch_Submit
            """).IgnoreWhiteSpace);
        
        // --- same without traces
        device.EnableTraces  = false;
        device.ClearTraces();
        
        Pattern.MultiplyAddKernel(weight.In,  input.In, 42,   output.InOut);
        Pattern.MultiplyAddKernel(weight.In,  output.In, 42,  input.InOut);
        
        device.Download();
        Assert.AreEqual("calls: 2  passes: 2  hazards: 2", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 2  passes: 2  hazards: 2) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            """).IgnoreWhiteSpace);
        
        Assert.That(device.KernelMetricLog, Contains.Substring("MultiplyAddKernel()             calls: 4  passes: 4"));
        device.ClearKernelMetrics();
        Assert.AreEqual("--- KERNEL METRIC ---", device.KernelMetricLog);
    }
    
    [Kernel] [OmitHash]
    private static void Assign([Span] ref float output, [Span] float input) {
        output = input;
    }
    
    [Test]
    public void Test_GPU_Hazard_WAW_Split()
    {
        using var device = Device;

        using var weight   = device.CreateBuffer<float>(100, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer<float>(100, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer<float>(100, "output", BufferProfile.InOut);
        
        device.EnableTraces  = true;
        device.EnablePassBatching = true;
        device.ClearKernelMetrics();
        
        Pattern.MultiplyAddKernel(weight.In, input.In, 42, output.InOut);
        
        AssignKernel(output.InOut, input.In);
        Pattern.MultiplyAddKernel(weight.In, input.In, 23, output.InOut);
        
        device.Download();
        
        Assert.AreEqual("calls: 3  passes: 3  hazards: 4", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 3  passes: 3  hazards: 4) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  1   new_pass
              | WAW 'output'
              | RAW 'input'
            AssignKernel()                  calls:  1   pass_split
              | RAW 'input'
              | WAW 'output'
            MultiplyAddKernel()             calls:  1   pass_split
            > Kernel_Submit
            > Batch_Submit
            """).IgnoreWhiteSpace);
        Assert.That(device.KernelMetricLog, Contains.Substring("MultiplyAddKernel()             calls: 2  passes: 2"));
        Assert.That(device.KernelMetricLog, Contains.Substring("AssignKernel()                  calls: 1  passes: 1"));
    }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Fusion_of_Views()
    {
        using var device = Device;

        using var weight   = device.CreateBuffer<float>(100, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer<float>(100, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer<float>(100, "output", BufferProfile.InOut);
        
        device.EnableTraces  = true;
        device.EnablePassBatching = true;
        
        Pattern.MultiplyAddKernel(weight.AsReadOnly(0, 10),  input.AsReadOnly(0, 10),   42,  output.Slice(0, 10));
        Pattern.MultiplyAddKernel(weight.AsReadOnly(0, 10),  output.AsReadOnly(10, 10), 42,  input.Slice(10, 10));
        AssignKernel(output.Slice(20,10), input.AsReadOnly(20, 10));
        Pattern.MultiplyAddKernel(weight.AsReadOnly(0, 10),  output.AsReadOnly(30, 10), 42,  input.Slice(30, 10));
        
        device.Download();
        Assert.AreEqual("calls: 4  passes: 1  hazards: 0", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 4  passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  2   new_pass
            AssignKernel()                  calls:  1
            MultiplyAddKernel()             calls:  1
            > Kernel_Submit
            > Batch_Submit
            """).IgnoreWhiteSpace);
    }
    
    [Kernel] [OmitHash]
    private static void ReadOnly([Span] float input) { }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Fusion_of_ReadOnly_Views()
    {
        using var device = Device;

        using var input    = device.CreateBuffer<float>(100, "input",  BufferProfile.StaticIn);
        
        device.EnableTraces  = true;
        device.EnablePassBatching = true;
        
        ReadOnlyKernel(input.In);
        ReadOnlyKernel(input.In);
        ReadOnlyKernel(input.In);
        
        device.Download();
        Assert.AreEqual("calls: 3  passes: 1  hazards: 0", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 3   passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            ReadOnlyKernel()                calls:  3   new_pass
            > Kernel_Submit
            > Batch_Submit
            """).IgnoreWhiteSpace);
    }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Fusion_of_Disjoint_Buffers()
    {
        using var device = Device;

        using var weight    = device.CreateBuffer<float>(100, "weight",   BufferProfile.StaticIn);
        using var inputA    = device.CreateBuffer<float>(100, "inputA",   BufferProfile.InOut);
        using var outputA   = device.CreateBuffer<float>(100, "outputA",  BufferProfile.InOut);
        
        using var inputB    = device.CreateBuffer<float>(100, "inputB",   BufferProfile.InOut);
        using var outputB   = device.CreateBuffer<float>(100, "outputB",  BufferProfile.InOut);
        
        device.EnableTraces  = true;
        device.EnablePassBatching = true;
        
        Pattern.MultiplyAddKernel(weight.In, inputA.InOut, 42, outputA.InOut);
        Pattern.MultiplyAddKernel(weight.In, inputB.InOut, 42, outputB.InOut);
        
        device.Download();
        Assert.AreEqual("calls: 2  passes: 1  hazards: 0", device.Stats.ToString());
        Assert.That(device.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: True  calls: 2   passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  2   new_pass
            > Kernel_Submit
            > Batch_Submit
            """).IgnoreWhiteSpace);
    }
}