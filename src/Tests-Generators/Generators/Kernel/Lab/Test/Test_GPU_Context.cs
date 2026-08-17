using System;
using System.Threading;
using Friflo.GPU;
using NUnit.Framework;
using NUnit.Framework.Legacy;

// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable InconsistentNaming
namespace Kernel.Lab;


public class Test_GPU_Context : KernelBase
{
    [Test]
    public void Test_GPU_Context_BeginContext_Exception()
    {
        using var device    = Device;
        using var context   = device.BeginContext();

        var e = Assert.Throws<InvalidOperationException>(() => {
            device.BeginContext();
        });
        StringAssert.StartsWith("[Context Conflict] A PipelineContext is already active on this thread.", e!.Message);
        StringAssert.Contains("Test_GPU_Context.cs:18", e!.Message);
    }
    
    [Test]
    public void Test_GPU_Context_Context_Leak()
    {
        if (Backend != TestBackend.WGPU) return;
        
        var device = Adapter.CreateDevice("GpuTestBase", UniformBufferSize);
        var context = device.BeginContext(); // context leak
        
        var e = Assert.Throws<InvalidOperationException>(() => {
            device.Dispose();
        });
        StringAssert.StartsWith("[Resource Leak Detected] GpuDevice.Dispose() failed because active PipelineContexts were not closed!\n  -> Left Context open on Thread:", e!.Message);
        StringAssert.Contains("Test_GPU_Context.cs:33", e!.Message);
        
        // cleanup as intended - otherwise leak detection will fail the test
        context.Dispose();      
        device.Dispose(); 
    }
    
    [Test]
    public void Test_GPU_Context_Missing_Device_Exception()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device = Device;
        using var weight   = device.CreateBuffer(10, 1f, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer(10, 2f, "input",  BufferProfile.StaticIn);
        using var output   = device.CreateBuffer(10, 3f, "output", BufferProfile.InOut);
        
        var e = Assert.Throws<InvalidOperationException>(() => {
            Pattern.MultiplyAddKernel(weight.In(), input.In(), 123, output.InOut());
        });
        Assert.AreEqual("Missing Device Context: 'GpuTestBase'. Call:  using var context = device.BeginContext();  before calling kernel method.", e!.Message);
    }
    
    [Test]
    public void Test_GPU_Context_Dispose_Context_Null()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device    = Device;
        
        Assert.IsNull(device.Context);
        
        var context = device.BeginContext();
        
        Assert.IsFalse(context.IsDisposed);
        Assert.AreSame(context, device.Context);

        context.Dispose();
        
        Assert.IsTrue(context.IsDisposed);
        Assert.IsNull(device.Context);
        
        var e = Assert.Throws<ObjectDisposedException>(() => {
            _ = context.Stats;
        });
        Assert.AreEqual(nameof(PipelineContext), e!.ObjectName);
        Assert.That(e!.Message, Is.EqualTo(
            """
            PipelineContext already disposed - Was used on device: GpuTestBase
            Object name: 'PipelineContext'.
            """).IgnoreWhiteSpace);
    }
    
    [Test]
    public void Test_GPU_Context_reuse_disposed_context()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device    = Device;
        using var weight    = device.CreateBuffer(100, 1f, "weight", BufferProfile.StaticIn);
        using var input     = device.CreateBuffer(100, 2f, "input",  BufferProfile.InOut);
        using var output    = device.CreateBuffer(100, 3f, "output", BufferProfile.InOut);
        
        using (var context1 = device.BeginContext()) {
            context1.PassBatching = PassBatching.None;
            context1.EnableTraces = true;
            
            Pattern.MultiplyAddKernel(weight.In(),  input.In(),  42,  output.InOut().Read());
            
            context1.Queue.ReadBuffers();
            
            var stats = context1.Stats;
            
            Assert.AreEqual(1, stats.Calls);
            Assert.AreEqual(1, stats.Passes);
            Assert.AreEqual(2, context1.Traces.Length);
            // Assert.AreEqual(1, context1.KernelMetrics.Length);   // TODO - check - should be device global
        }
        {
            // -- context2 is now reusing context1 instance
            using var context2 = device.BeginContext();
            
            Assert.AreEqual(PassBatching.HazardDriven, context2.PassBatching);
            Assert.IsFalse(context2.EnableTraces);
            
            var stats = context2.Stats;
            Assert.AreEqual(0, stats.Calls);
            Assert.AreEqual(0, stats.Passes);
            Assert.AreEqual(0, context2.Traces.Length);
            // Assert.AreEqual(0, context2.KernelMetrics.Length);   // TODO - check - should be device global
        }
    }
    
    [Test]
    public void Test_GPU_Context_Thread_Exceptions()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device            = Device;
        using var pipelineContext   = device.BeginContext();
        
        var thread = new Thread((obj) => {
            var context = (PipelineContext)obj!;
            AssertThreadException(() => _ = context.PassBatching);
            AssertThreadException(() =>     context.PassBatching = PassBatching.HazardDriven);
            AssertThreadException(() => _ = context.EnableTraces);
            AssertThreadException(() =>     context.EnableTraces = true);
            AssertThreadException(() => _ = context.TraceLog);
            AssertThreadException(() => _ = context.KernelMetricLog);
            AssertThreadException(() => _ = context.Stats);
            AssertThreadException(() => _ = context.Traces);
            AssertThreadException(() => _ = context.EnableTraces);
            AssertThreadException(() => _ = context.KernelMetrics);
            AssertThreadException(() => _ = context.Queue.Stats);
            
            AssertThreadException(() => context.ClearTraces());
            AssertThreadException(() => context.ClearKernelMetrics());
        //  AssertThreadException(() => context.NewPass());
            AssertThreadException(() => context.Queue.ReadBuffers());
        //  AssertThreadException(() => context.Flush());
        //  AssertThreadException(() => context.Synchronize());
            
        });
        thread.Start(pipelineContext);
        thread.Join();
    }
    
    private static void AssertThreadException(TestDelegate code)
    {
        var e = Assert.Throws<InvalidOperationException>(code);
        StringAssert.StartsWith("[Thread Context Violation] method executes on thread:", e!.Message);
    }
}