using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.Utils;

// ReSharper disable ConvertToUsingDeclaration
// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable InconsistentNaming
namespace Kernel.Lab;


public partial class Test_GPU_Queue : KernelBase
{
    
    [Kernel, Vectorize] [OmitHash]
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
        
        AssignKernel(target.InOut.StageRead(), source.In);
        
        context.PassBatching = PassBatching.HazardDriven;
        
        AssignKernel(target.InOut.StageRead(), source.In);
        
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
        
        AssignKernel(target.InOut.StageRead(), source.In);
        
        context.FlushTo(device);
        
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
        
        AssignKernel(target.Slice(2, 1).StageRead(), source.AsReadOnly(2, 1));
        AssignKernel(target.Slice(6, 1).StageRead(), source.AsReadOnly(6, 1));
        AssignKernel(target.Slice(0, 1).StageRead(), source.AsReadOnly(0, 1));
        AssignKernel(target.Slice(9, 1).StageRead(), source.AsReadOnly(9, 1));
        
        context.FlushTo(device);
        
        var queueStats = context.Queue.Stats;
        Assert.AreEqual(0, queueStats.Commands);
        Assert.AreEqual(0, queueStats.Ranges);
        
        device.Queue.ReadBuffers();
        
        Assert.AreEqual(new float[] { 1, 0, 3, 0, 0,  0, 7, 0, 0, 10 }, targetArr);
    }
    
    [Test]
    public void Test_GPU_Queue_Empty_FlushTo()
    {
        using var device = Device;
        using var context = device.BeginContext();
        
        context.FlushTo(device);
        
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
        
        // --- force one time allocations
        using (var context = device.BeginContext())
        {
            context.PassBatching = PassBatching.None;
            AssignKernel(target.InOut.StageRead(), source.In);
            context.PassBatching = PassBatching.HazardDriven;
            AssignKernel(target.InOut.StageRead(), source.In);
            
            context.Queue.ReadBuffers();
            Assert.AreEqual(sourceArr, targetArr);
        }
        
        // --- no allocation expected
        var startAll = Mem.GetAllocatedBytes();
        using (var context = device.BeginContext())
        {
            {
                var start = Mem.GetAllocatedBytes();
                context.PassBatching = PassBatching.None;
                AssignKernel(target.InOut.StageRead(), source.In);
                Mem.AssertNoAlloc(start);
            } {
                var start = Mem.GetAllocatedBytes();
                context.PassBatching = PassBatching.HazardDriven;
                AssignKernel(target.InOut.StageRead(), source.In);
                Mem.AssertNoAlloc(start);
            } {
                var start = Mem.GetAllocatedBytes();
                context.Queue.ReadBuffers();
                Mem.AssertNoAlloc(start); 
                // Mem.AssertAlloc(start, 40);  // when using ConcurrentStack<CommandList>.Push() - allocates Node: 40 bytes 
            }
        }
        Mem.AssertNoAlloc(startAll);
        Assert.AreEqual(sourceArr, targetArr);
    }
}