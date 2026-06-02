using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;

public class Test_GPU_Misc : KernelBase
{
    [Test]
    public void Test_GPU_Misc_Buffer_ToString()
    {
        using var staticIn  = Device.CreateBuffer(64, 1f, "StaticIn", BufferProfile.StaticIn);
        using var inOut     = Device.CreateBuffer(64, 2f, "InOut",    BufferProfile.InOut);
        
        var inOutView  = inOut.Slice(10,10);
        var staticView = staticIn.AsReadOnly(10,10);
        
        Assert.AreEqual("BufferView<float> 'InOut' [10..20]",       inOutView.ToString());
        Assert.AreEqual("ReadOnlyView<float> 'StaticIn' [10..20]",  staticView.ToString());
        
        Buffer  <float> inOutBuffer  = inOutView;
        InBuffer<float> staticBuffer = staticView;
        
        Assert.AreEqual("GpuBuffer<float> 'InOut'  Length: 10",     inOutBuffer.ToString());
        Assert.AreEqual("GpuBuffer<float> 'StaticIn'  Length: 10",  staticBuffer.ToString());
    }
    
    [Test]
    public void Test_GPU_Adapter()
    {
        using var gpuWeight   = Device.CreateBuffer<float>(100, 0, "test-buffer", BufferProfile.StaticIn);
        Assert.AreEqual("test-buffer",  gpuWeight.Label);
        Assert.AreEqual(100,            gpuWeight.Length);
        
        Assert.NotNull(Instance.GetAdapterInfos());
        
        var info = Adapter.GetAdapterInfo();
        Console.WriteLine($"Adapter: {info.AdapterType}  Backend: {info.BackendType}  Name: {info.Name}  Driver: {info.DriverDescription}  VendorID: {info.VendorID}  DeviceID: {info.DeviceID}");
        
        Assert.AreEqual("GpuTestBase", Device.Label);
        
        var adapterLimits = Adapter.GetAdapterLimits();
        Console.WriteLine($@"
MaxStorageBufferBindingSize:        {adapterLimits.MaxStorageBufferBindingSize}
MaxComputeWorkgroupStorageSize:     {adapterLimits.MaxComputeWorkgroupStorageSize}
MaxBindGroups:                      {adapterLimits.MaxBindGroups}
MaxComputeInvocationsPerWorkgroup:  {adapterLimits.MaxComputeInvocationsPerWorkgroup}");
        
        _ = Device.GetDeviceLimits();
        
    }
}