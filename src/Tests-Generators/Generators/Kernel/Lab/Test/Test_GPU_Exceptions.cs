using System;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using NUnit.Framework;
using NUnit.Framework.Legacy;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;

public class Test_GPU_Exceptions : KernelBase
{
    [Test]
    public void Test_GPU_Exceptions_GpuBuffer()
    {
        Assert.IsFalse(Instance.IsDisposed);
        Assert.IsFalse(Adapter.IsDisposed);
        
        using var device1   = Adapter.CreateDevice("device1");
        using var device2   = Adapter.CreateDevice("device2");
        Assert.AreEqual("device1: Alive", device1.ToString());
        
        var weight  = new float[64]; // no alignment
        var input   = new float[64];
        var output  = new float[64];
        for (int n = 0; n < 64; ++n) {
            weight[n] = n;
            input[n]  = n + 1000;
        }
        using var gpuWeight   = device1.CreateBuffer(weight, "gpuWeight", BufferProfile.StaticIn);
        using var gpuInput    = device1.CreateBuffer(input,  "gpuInput",  BufferProfile.StaticIn);
        using var gpuOutput   = device1.CreateBuffer(output, "gpuOutput", BufferProfile.InOut);
        
        StringAssert.StartsWith("gpuWeight(", gpuWeight.ToString());
        StringAssert.EndsWith  ("): Alive",   gpuWeight.ToString());
        Assert.IsFalse(gpuWeight.IsDisposed);

        { 
            var gpuOutput2   = device1.CreateBuffer(output, "gpuOutput2", BufferProfile.InOut);
            gpuOutput2.Dispose();
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput2.InOut);
            });
            StringAssert.StartsWith("Existential Void:", e!.Message!);
        } {
            using var gpuOutput2 = device2.CreateBuffer(input, "gpuOutput2", BufferProfile.StaticIn);
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput2.InOut);
            });
            StringAssert.StartsWith("Diplomatic Incident:", e!.Message!);
        } {
            using var gpuOutputSmall = device1.CreateBuffer(new float[63], "gpuOutput1", BufferProfile.StaticIn);
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutputSmall.InOut);
            });
            StringAssert.StartsWith("Totalitarian Sizing:", e!.Message!);
        } {
            using var gpuWeight2 = device2.CreateBuffer(weight, "gpuWeight2", BufferProfile.StaticIn); 
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight2.In, input, 42, output);
            });
            StringAssert.StartsWith("Identity Crisis:", e!.Message!);
        }  {
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(weight, gpuInput.In, 42, output);
            });
            StringAssert.StartsWith("Identity Crisis:", e!.Message!);
        } {
            using var instance  = new CpuInstance();
            using var adapter   = instance.CreateAdapter(GpuBackendType.Scalar);
            using var device    = adapter.CreateDevice("Scalar");
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(weight, input, 42, output, ComputeMode.GPU);
            });
            StringAssert.StartsWith("The Ghost Orchestra: ", e!.Message!);
        }
        GpuPattern.ShadowMethod(weight, input, 42, output); // using only spans
        GpuPattern.ShadowMethod(weight, input, 42, output); // using only spans
        
        using var gpuOutput3   = device1.CreateBuffer(output, "gpuOutput3", BufferProfile.InOut);
         // gpuOutput3.InOut can also be used for InBuffer<float> parameter
        GpuPattern.ShadowMethod(gpuWeight.In, gpuOutput3.InOut, 42, gpuOutput.InOut);
        {
            using var gpuOutput1 = device1.CreateBuffer(input, "gpuOutput1", BufferProfile.StaticIn);
            device1.Dispose();
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
            });
            StringAssert.StartsWith("Archaeological Error:", e!.Message!);
        }
    }
    
    [Test]
    public void Test_GPU_Exceptions_conflicting_usages()
    {        
        using var device    = Adapter.CreateDevice("device");
        if (device.DefaultComputeMode != ComputeMode.GPU) return;
        
        using var gpuWeight = device.CreateBuffer<float>(64, "gpuWeight", BufferProfile.StaticIn);
        using var gpuOutput = device.CreateBuffer<float>(64, "gpuOutput", BufferProfile.InOut);

        var e = Assert.Throws<InvalidOperationException>(() => {
            GpuPattern.ShadowMethod(gpuWeight.In, gpuOutput.In, 42, gpuOutput.InOut);
        })!;
        StringAssert.StartsWith("Schrödinger's Buffer:", e!.Message!);
    }
    
    [Test]
    public void Test_GPU_Exceptions_conflicting_usages_wgpu()
    {        
        using var device    = Adapter.CreateDevice("device");
        if (device.DefaultComputeMode != ComputeMode.GPU) return;
        
        using var gpuWeight = device.CreateBuffer<float>(64, "gpuWeight", BufferProfile.StaticIn);
        using var gpuOutput = device.CreateBuffer<float>(64, "gpuOutput", BufferProfile.InOut);
        
        var inputSlice   = gpuOutput.Slice(0, 10);
        var outputSlice1 = gpuOutput.Slice(0, 10);
        var outputSlice2 = gpuOutput.Slice(20,10);
        
        device.PipelineContext.EnablePassBatching = false;

        var e = Assert.Throws<WgpuException>(() => {
            ExpectedCommandBuffers++; // Symptom of root cause error
            GpuPattern.ShadowMethod(inputSlice, outputSlice1, 42, outputSlice2);
        })!;
        StringAssert.Contains("gpuOutput",          e.Message);
        StringAssert.Contains("conflicting usages", e.Message);
        StringAssert.Contains("STORAGE_READ_WRITE", e.Message);
    }
    
    
    [Test]
    public void Test_GPU_Repeat()
    {
        {
            using var device = Device;

            var weight  = new float[64]; // no alignment
            var input   = new float[64];
            var output  = new float[64];
            for (int n = 0; n < 64; ++n) {
                weight[n] = n;
                input[n]  = n + 1000;
            }
            using var gpuWeight   = device.CreateBuffer(weight, "gpuWeight", BufferProfile.StaticIn);
            using var gpuInput    = device.CreateBuffer(input,  "gpuInput",  BufferProfile.StaticIn);
            using var gpuOutput   = device.CreateBuffer(output, "gpuOutput", BufferProfile.InOut);
            
            for (int n = 0; n < 5; ++n) {
                GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
                Console.WriteLine(HandleDiff.GetState());
            }
            device.Download();
        }
        Console.WriteLine(HandleDiff.GetState());
    }
    
    [Test]
    public void Test_GPU_Adapter()
    {
        using var gpuWeight   = Device.CreateBuffer<float>(100, "test-buffer", BufferProfile.StaticIn);
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