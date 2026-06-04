using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.Utils;

// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable InconsistentNaming
namespace Kernel.Lab;


public partial class Test_GPU_Queue : KernelBase
{
    
    [Kernel] [OmitHash]
    private static void Assign([Span] ref float output, [Span] float input) {
        output = input;
    }
    
    [Test]
    public void Test_GPU_Queue_ReadBuffers()
    {
        var sourceArr = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var targetArr = new float[10];
        using var device = Device;
        using var source   = device.CreateBuffer(sourceArr, "source", BufferProfile.StaticIn);
        using var target   = device.CreateBuffer(targetArr, "target", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.PassBatching = PassBatching.None;
        
        AssignKernel(target.InOut, source.In);
        
        context.PassBatching = PassBatching.HazardDriven;
        
        AssignKernel(target.InOut, source.In);
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual(sourceArr, targetArr);
    }
    
    [Test]
    public void Test_GPU_Queue_FlushTo()
    {
        var sourceArr = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var targetArr = new float[10];
        using var device = Device;
        using var source   = device.CreateBuffer(sourceArr, "source", BufferProfile.StaticIn);
        using var target   = device.CreateBuffer(targetArr, "target", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        
        AssignKernel(target.InOut, source.In);
        
        context.FlushQueueTo(device);
        
        device.Queue.ReadBuffers();
        
        Assert.AreEqual(sourceArr, targetArr);
    }
    
    [Test]
    public void Test_GPU_Queue_Batching_FlushTo()
    {
        var sourceArr = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var targetArr = new float[10];
        using var device = Device;
        using var source   = device.CreateBuffer(sourceArr, "source", BufferProfile.StaticIn);
        using var target   = device.CreateBuffer(targetArr, "target", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        context.PassBatching = PassBatching.HazardDriven;
        
        AssignKernel(target.InOut, source.In);
        
        context.FlushQueueTo(device);
        
        var queueStats = context.Queue.Stats;
        Assert.AreEqual(0, queueStats.Commands);
        Assert.AreEqual(0, queueStats.Ranges);
        
        device.Queue.ReadBuffers();
        
        Assert.AreEqual(sourceArr, targetArr);
    }
    
    [Test]
    public void Test_GPU_Queue_Empty_FlushTo()
    {
        using var device = Device;
        using var context = device.BeginContext();
        
        context.FlushQueueTo(device);
        
        device.Queue.ReadBuffers();
    }
    
    [Test]
    public void Test_GPU_Queue_Zero_Alloc()
    {
        var sourceArr = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var targetArr = new float[10];
        using var device = Device;
        using var source   = device.CreateBuffer(sourceArr, "source", BufferProfile.StaticIn);
        using var target   = device.CreateBuffer(targetArr, "target", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        
        // --- force one time allocations
        context.PassBatching = PassBatching.None;
        AssignKernel(target.InOut, source.In);
        context.PassBatching = PassBatching.HazardDriven;
        AssignKernel(target.InOut, source.In);
        
        context.Queue.ReadBuffers();
        Assert.AreEqual(sourceArr, targetArr);
        
        // --- no allocation expected
        {
            var start = Mem.GetAllocatedBytes();
            context.PassBatching = PassBatching.None;
            AssignKernel(target.InOut, source.In);
            Mem.AssertNoAlloc(start);
        } {
            var start = Mem.GetAllocatedBytes();
            context.PassBatching = PassBatching.HazardDriven;
            AssignKernel(target.InOut, source.In);
            Mem.AssertNoAlloc(start);
        } {
            var start = Mem.GetAllocatedBytes();
            context.Queue.ReadBuffers();
            Mem.AssertAlloc(start, 40);         // 40 bytes -> ConcurrentStack<CommandList>.Push()
            // Mem.AssertNoAlloc(start); 
        }
        Assert.AreEqual(sourceArr, targetArr);
    }
}