using System;
using System.Threading;
using Friflo.Vectorization.GPU;
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
        
        {
            var e = Assert.Throws<InvalidOperationException>(() => {
                device.BeginContext();
            });
            StringAssert.StartsWith("[Context Conflict] A PipelineContext is already active on this thread.", e!.Message);
        }
    }
    
    [Test]
    public void Test_GPU_Context_Exceptions()
    {
        using var device            = Device;
        using var pipelineContext   = device.BeginContext();
        
        var thread = new Thread((obj) => {
            var context = (PipelineContext)obj!;
            AssertThreadException(() => _ = context.PassBatching);
            AssertThreadException(() => _ = context.EnableTraces);
            AssertThreadException(() => _ = context.TraceLog);
            AssertThreadException(() => _ = context.KernelMetricLog);
            AssertThreadException(() => _ = context.Stats);
            AssertThreadException(() => _ = context.Traces);
            AssertThreadException(() => _ = context.EnableTraces);
            AssertThreadException(() => _ = context.KernelMetrics);
            
            AssertThreadException(() => context.ClearTraces());
            AssertThreadException(() => context.ClearKernelMetrics());
        //  AssertThreadException(() => context.NewPass());
            AssertThreadException(() => context.Download());
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