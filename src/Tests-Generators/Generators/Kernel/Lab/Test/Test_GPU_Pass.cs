using System.Collections.Generic;
using System.Diagnostics;
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

        using var gpuWeight   = device.CreateBuffer(64, 1f, "gpuWeight", BufferProfile.StaticIn);
        using var gpuInput    = device.CreateBuffer(64, 2f, "gpuInput",  BufferProfile.StaticIn);
        using var gpuOutput   = device.CreateBuffer(64, 3f, "gpuOutput", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        _ = context.KernelMetrics;
        
        for (int n = 0; n < 5; ++n) {
            Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        }
        context.Queue.ReadBuffers();
        
        AssertResult(gpuOutput.InOut, 44f);
        Assert.AreEqual(2,              context.Traces.Length);
        Assert.AreEqual("MultiplyAddKernel", context.Traces[0].KernelName);
        Assert.AreEqual("calls: 5  passes: 1  hazards: 0", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 5   passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  5   new_pass
            > Submit                        commands: 1
            """).IgnoreWhiteSpace);
        
        // --- same without traces
        context.EnableTraces  = false;
        context.ClearTraces();
        
        for (int n = 0; n < 5; ++n) {
            Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        }
        context.Queue.ReadBuffers();
        
        AssertResult(gpuOutput.InOut, 44f);
        Assert.AreEqual(0, context.Traces.Length);
        Assert.AreEqual("calls: 5  passes: 1  hazards: 0", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 5  passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            """).IgnoreWhiteSpace);
        
        // --- Force hazards
        context.PassBatching    = PassBatching.None;
        context.EnableTraces    = true;
        context.ClearTraces();

        Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        Pattern.MultiplyAddKernel(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);

        Assert.AreEqual(2, context.Queue.Stats.Commands);
        Assert.AreEqual(2, context.Queue.Stats.Ranges);

        context.Queue.ReadBuffers();
        
        AssertResult(gpuOutput.InOut, 44f);
        Assert.AreEqual(0, context.Queue.Stats.Commands);
        Assert.AreEqual(0, context.Queue.Stats.Ranges);
        Assert.AreEqual("calls: 2  passes: 2  hazards: 0", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: None  calls: 2   passes: 2  hazards: 0) ---
            MultiplyAddKernel()             calls:  1   new_pass
            MultiplyAddKernel()             calls:  1   new_pass
            > Submit                        commands: 3
            """).IgnoreWhiteSpace);
    }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Split() // Read-After-Write  &  Write-After-Read
    {
        using var device = Device;

        using var weight   = device.CreateBuffer(100, 1f, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer(100, 2f, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer(100, 3f, "output", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        
        Pattern.MultiplyAddKernel(weight.In,  input.In,  42,  output.InOut);
        Pattern.MultiplyAddKernel(weight.In,  output.In, 42,  input.InOut);
        
        context.Queue.ReadBuffers();
        
        AssertResult(input.InOut, 86f);
        Assert.AreEqual("calls: 2  passes: 2  hazards: 2", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 2   passes: 2  hazards: 2) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  1   new_pass
              | RAW 'output'
              | WAR 'input'
            MultiplyAddKernel()             calls:  1   pass_split
            > Submit                        commands: 1
            """).IgnoreWhiteSpace);
        
        // --- same without traces
        context.EnableTraces  = false;
        context.ClearTraces();
        
        Pattern.MultiplyAddKernel(weight.In,  input.In, 42,   output.InOut);
        Pattern.MultiplyAddKernel(weight.In,  output.In, 42,  input.InOut);
        
        context.Queue.ReadBuffers();
        
        AssertResult(input.InOut, 170f);
        Assert.AreEqual("calls: 2  passes: 2  hazards: 2", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 2  passes: 2  hazards: 2) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            """).IgnoreWhiteSpace);
        Assert.That(context.KernelMetricLog, Is.EqualTo(
            """
            --- KERNEL METRIC ---
            MultiplyAddKernel()             calls: 4  passes: 4
            """).IgnoreWhiteSpace);
        context.ClearKernelMetrics();
        Assert.AreEqual("--- KERNEL METRIC ---", context.KernelMetricLog);
    }
    
    [Kernel] [OmitHash]
    private static void Assign([Span] ref float output, [Span] float input) {
        output = input;
    }
    
    [Test]
    public void Test_GPU_Hazard_WAW_Split()
    {
        using var device = Device;

        using var weight   = device.CreateBuffer(100, 1f, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer(100, 2f, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer(100, 3f, "output", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        context.ClearKernelMetrics();
        
        Pattern.MultiplyAddKernel(weight.In, input.In, 42, output.InOut);
        
        AssignKernel(output.InOut, input.In);
        Pattern.MultiplyAddKernel(weight.In, input.In, 23, output.InOut);
        
        context.Queue.ReadBuffers();
        
        AssertResult(output.InOut, 25f);
        Assert.AreEqual("calls: 3  passes: 3  hazards: 4", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 3   passes: 3  hazards: 4) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  1   new_pass
              | WAW 'output'
              | RAW 'input'
            AssignKernel()                  calls:  1   pass_split
              | RAW 'input'
              | WAW 'output'
            MultiplyAddKernel()             calls:  1   pass_split
            > Submit                        commands: 1
            """).IgnoreWhiteSpace);
        Assert.That(context.KernelMetricLog, Is.EqualTo(
            """
            --- KERNEL METRIC ---
            AssignKernel()                  calls: 1  passes: 1
            MultiplyAddKernel()             calls: 2  passes: 2
            """).IgnoreWhiteSpace);
    }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Fusion_of_Views()
    {
        using var device = Device;

        using var weight   = device.CreateBuffer(100, 1f, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer(100, 2f, "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer(100, 3f, "output", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        
        Pattern.MultiplyAddKernel(weight.AsReadOnly(0, 10),  input.AsReadOnly(0, 10),   42,  output.Slice(0, 10));
        Pattern.MultiplyAddKernel(weight.AsReadOnly(0, 10),  output.AsReadOnly(10, 10), 42,  input.Slice(10, 10));
        AssignKernel(output.Slice(20,10), input.AsReadOnly(20, 10));
        Pattern.MultiplyAddKernel(weight.AsReadOnly(0, 10),  output.AsReadOnly(30, 10), 42,  input.Slice(30, 10));
        
        context.Queue.ReadBuffers();
        
        AssertResult(input.Slice(30, 10), 45f);
        Assert.AreEqual("calls: 4  passes: 1  hazards: 0", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 4   passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  2   new_pass
            AssignKernel()                  calls:  1
            MultiplyAddKernel()             calls:  1
            > Submit                        commands: 1
            """).IgnoreWhiteSpace);
    }
    
    [Kernel] [OmitHash]
    private static void ReadOnly([Span] float input) { }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Fusion_of_ReadOnly_Views()
    {
        using var device = Device;

        using var input    = device.CreateBuffer(100, 1f, "input",  BufferProfile.StaticIn);
        
        using var context = device.BeginContext();
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        
        ReadOnlyKernel(input.In);
        ReadOnlyKernel(input.In);
        ReadOnlyKernel(input.In);
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual("calls: 3  passes: 1  hazards: 0", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 3   passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            ReadOnlyKernel()                calls:  3   new_pass
            > Submit                        commands: 1
            """).IgnoreWhiteSpace);
    }
    
    [Test]
    public void Test_GPU_Hazard_Pass_Fusion_of_Disjoint_Buffers()
    {
        using var device = Device;

        using var weight    = device.CreateBuffer(100, 1f, "weight",   BufferProfile.StaticIn);
        using var inputA    = device.CreateBuffer(100, 2f, "inputA",   BufferProfile.InOut);
        using var outputA   = device.CreateBuffer(100, 3f, "outputA",  BufferProfile.InOut);
        
        using var inputB    = device.CreateBuffer(100, 4f, "inputB",   BufferProfile.InOut);
        using var outputB   = device.CreateBuffer(100, 5f, "outputB",  BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        
        Pattern.MultiplyAddKernel(weight.In, inputA.InOut, 42, outputA.InOut);
        Pattern.MultiplyAddKernel(weight.In, inputB.InOut, 42, outputB.InOut);
        
        context.Queue.ReadBuffers();
        
        AssertResult(outputB.InOut, 46f);
        Assert.AreEqual("calls: 2  passes: 1  hazards: 0", context.Stats.ToString());
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 2   passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            MultiplyAddKernel()             calls:  2   new_pass
            > Submit                        commands: 1
            """).IgnoreWhiteSpace);
    }
    
    [StackTraceHidden]
    private static void AssertResult<T>(InBuffer<T> buffer, T expect) where T : unmanaged
    {
        foreach (var value in buffer.Span) {
            if (!EqualityComparer<T>.Default.Equals(expect, value)) {
                Assert.Fail($"expect: {expect}  was:  {value}");
            }
        }
    }
}