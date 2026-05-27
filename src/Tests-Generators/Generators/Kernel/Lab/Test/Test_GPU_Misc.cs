using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;

public class Test_GPU_Misc : KernelBase
{
    [Test]
    public void Test_GPU_Misc_Buffer_ToString()
    {
        using var staticIn  = Device.CreateBuffer<float>(64, "StaticIn", BufferProfile.StaticIn);
        using var inOut     = Device.CreateBuffer<float>(64, "InOut",    BufferProfile.InOut);
        
        var inOutView  = inOut.Slice(10,10);
        var staticView = staticIn.AsReadOnly(10,10);
        
        Assert.AreEqual("BufferView<float> 'InOut' [10..20]",       inOutView.ToString());
        Assert.AreEqual("ReadOnlyView<float> 'StaticIn' [10..20]",  staticView.ToString());
        
        Buffer  <float> inOutBuffer  = inOutView;
        InBuffer<float> staticBuffer = staticView;
        
        Assert.AreEqual("GpuBuffer<float> 'InOut'  Length: 10",     inOutBuffer.ToString());
        Assert.AreEqual("GpuBuffer<float> 'StaticIn'  Length: 10",  staticBuffer.ToString());
    }
}