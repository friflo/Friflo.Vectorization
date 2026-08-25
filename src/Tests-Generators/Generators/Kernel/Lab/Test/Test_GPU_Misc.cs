using System;
using System.Runtime.CompilerServices;
using Friflo.GPU;
using Friflo.GPU.Runtime;
using Friflo.WGPU;
using Friflo.WGPU.ImDraw;
using Friflo.WGPU.Runtime;
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
        
        var inOutView  = inOut.InOut(10,10);
        var staticView = staticIn.In(10,10);
        
        Assert.AreEqual("InOutView<float> 'InOut' [10..20]",       inOutView.ToString());
        Assert.AreEqual("InView<float> 'StaticIn' [10..20]",  staticView.ToString());
        
        InOutBuffer  <float> inOutBuffer  = inOutView;
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
    
    [Test]
    public void Test_GPU_Memory()
    {
        var weightMem   = new Memory<float>([1, 1, 1, 1]);
        var inputMem    = new Memory<float>([1, 2, 3, 4]);
        var outputMem   = new Memory<float>([0, 0, 0, 0]);
        
        using var device    = Device;
        using var weight   = device.CreateBuffer(weightMem, "weight", BufferProfile.StaticIn);
        using var input    = device.CreateBuffer(inputMem,  "input",  BufferProfile.InOut);
        using var output   = device.CreateBuffer(outputMem, "output", BufferProfile.InOut);

        using var context = device.BeginContext();
        
        Pattern.MultiplyAddKernel(weight.In(), input.In(), 42, output.InOut().Read());
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual(new float[] { 43, 44, 45, 46 }, outputMem.ToArray());
    }
    
    [Test]
    public void Test_GPU_SizeOf()
    {
        Assert.AreEqual(24, Unsafe.SizeOf<GpuBuffers>());
        Assert.AreEqual(56, Unsafe.SizeOf<ComputeCache>());
        Assert.AreEqual(64, Unsafe.SizeOf<PipelineCache>());
        
        Assert.AreEqual(56, Unsafe.SizeOf<RenderTarget>());
        Assert.AreEqual( 8, Unsafe.SizeOf<RenderPass>());
        Assert.AreEqual( 8, Unsafe.SizeOf<Draw2D>());
        Assert.AreEqual(32, Unsafe.SizeOf<Gui>());
    }
}