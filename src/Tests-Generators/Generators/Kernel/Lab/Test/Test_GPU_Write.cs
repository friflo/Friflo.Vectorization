using System;
using Friflo.Vectorization;
using Friflo.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;

public partial class Test_GPU_Write : KernelBase
{
    [Kernel, Vectorize] [OmitHash]
    private static void Assign([Span] ref float output, [Span] float input) {
        output = input;
    }
    
    [Test]
    public void Test_GPU_Write_view()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device    = Device;
        using var input     = device.CreateBuffer(100, 4f, "input",   BufferProfile.InOut);
        using var output    = device.CreateBuffer(100, 5f, "output",  BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        
        var outputView  = output.InOut(0, 10);
        var inputView   = input.In(0, 10);
        inputView.Span[0] = 40;
        inputView.Span[9] = 49;
        
        AssignKernel(outputView.Read(), inputView.Write());
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual(40, outputView.Span[0]);
        Assert.AreEqual(49, outputView.Span[9]);

        // --- update buffers again
        inputView.Span[0] = 50;
        inputView.Span[9] = 59;
        
        AssignKernel(outputView.Read(), inputView.Write());
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual(50, outputView.Span[0]);
        Assert.AreEqual(59, outputView.Span[9]);
    }
    
    [Test]
    public void Test_GPU_Write_views_coalescing()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device    = Device;
        using var input     = device.CreateBuffer(100, 4f, "input",   BufferProfile.InOut);
        using var output    = device.CreateBuffer(100, 5f, "output",  BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.EnableTraces    = true;
        context.PassBatching    = PassBatching.HazardDriven;
        
        var inputView1  = input.In(10, 1).Write();  inputView1.Span[0] = 40;
        var inputView2  = input.In(20, 1).Write();  inputView2.Span[0] = 41;
        var inputView3  = input.In(30, 1).Write();  inputView3.Span[0] = 42;
        
        var outputView1 = output.InOut(50, 1).Read();
        var outputView2 = output.InOut(51, 1).Read();
        var outputView3 = output.InOut(52, 1).Read();
        
        AssignKernel(outputView1, inputView1);  // coalescing of all Write() calls with a single wgpuQueueWriteBuffer()
        AssignKernel(outputView2, inputView2);
        AssignKernel(outputView3, inputView3);
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual(40, outputView1.Span[0]);
        Assert.AreEqual(41, outputView2.Span[0]);
        Assert.AreEqual(42, outputView3.Span[0]);
        
        Assert.That(context.TraceLog, Is.EqualTo(
            """
            --- PIPELINE TRACE (batching: HazardDriven  calls: 3   passes: 1  hazards: 0) ---
            --- Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
            > Write 'input'                 ranges: 1 - coalescing
            AssignKernel()                  calls:  3   new_pass
            > Submit                        commands: 1
            """).IgnoreWhiteSpace);
    }
    
    [Test]
    public void Test_GPU_Write_view_late_Write()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device    = Device;
        using var input     = device.CreateBuffer(100, 4f, "input",   BufferProfile.InOut);
        using var output    = device.CreateBuffer(100, 5f, "output",  BufferProfile.InOut);
        
        using var context = device.BeginContext();
        
        var outputView1  = output.InOut( 0, 1);
        var inputView1   = input.In(0, 1);
        inputView1.Span[0] = 40;
        
        AssignKernel(outputView1.Read(), inputView1.Write());   // Write() uses wgpuQueueWriteBuffer()
        
        var outputView2  = output.InOut(10, 1);
        var inputView2   = input.In(20, 1);
        inputView2.Span[0] = 50;
        
        AssignKernel(outputView2.Read(), inputView2.Write());
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual(40, outputView1.Span[0]);
        Assert.AreEqual(50, outputView2.Span[0]);
    }
    
    [Test]
    public void Test_GPU_Write_view_double_Write()
    {
        if (Backend != TestBackend.WGPU) return;
        
        using var device    = Device;
        using var input     = device.CreateBuffer(100, 4f, "input",   BufferProfile.InOut);
        using var output    = device.CreateBuffer(100, 5f, "output",  BufferProfile.InOut);
        
        using var context = device.BeginContext();
        
        var inputView = input.In(0, 1);
        inputView.Span[0] = 40;
        
        inputView.Write();  // OK
        var e = Assert.Throws<InvalidOperationException>(() => {
            inputView.Write();  // second Write()
        });
        Assert.AreEqual("a Write() of buffer view 'input'[0..1] is already queued. You must call Submit() before you can Write() the same view again.", e!.Message);
    }
}