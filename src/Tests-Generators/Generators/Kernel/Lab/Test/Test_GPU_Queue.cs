using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

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
    public void Test_GPU_Queue_TransferTo()
    {
        var sourceArr = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var targetArr = new float[10];
        using var device = Device;
        using var source   = device.CreateBuffer(sourceArr, "source", BufferProfile.StaticIn);
        using var target   = device.CreateBuffer(targetArr, "target", BufferProfile.InOut);
        
        using var context = device.BeginContext();
        
        AssignKernel(target.InOut, source.In);
        
        context.Queue.TransferTo(device.Queue);
        
        device.Queue.ReadBuffers();
        
        Assert.AreEqual(sourceArr, targetArr);
    }
    

}