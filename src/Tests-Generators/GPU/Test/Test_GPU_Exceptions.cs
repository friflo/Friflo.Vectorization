using System;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using NUnit.Framework.Legacy;

// ReSharper disable InconsistentNaming
namespace Tests.GPU;

public class Test_GPU_Exceptions : GpuTestBase
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
        using var gpuWeight   = device1.CreateBuffer(weight, GpuBufferUsage.Storage, "gpuWeight");
        using var gpuInput    = device1.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuInput");
        using var gpuOutput   = device1.CreateBuffer(output, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "gpuOutput");
        
        StringAssert.StartsWith("gpuWeight(", gpuWeight.ToString());
        StringAssert.EndsWith  ("): Alive",   gpuWeight.ToString());
        Assert.IsFalse(gpuWeight.IsDisposed);

        {   // Scope important to Dispose() result (=output)
            using var result = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
        } {
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
            });
            StringAssert.StartsWith("Existential Void:", e!.Message!);
        } {
            using var gpuOutput2 = device2.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuOutput2");
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput2);
            });
            StringAssert.StartsWith("Diplomatic Incident:", e!.Message!);
        } {
            using var gpuOutputSmall = device1.CreateBuffer(new float[63],  GpuBufferUsage.Storage, "gpuOutput1");
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutputSmall);
            });
            StringAssert.StartsWith("Totalitarian Sizing:", e!.Message!);
        } {
            using var gpuOutput1 = device1.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuOutput1");
            device1.Dispose();
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput1);
            });
            StringAssert.StartsWith("Archaeological Error:", e!.Message!);
        } {
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(weight, gpuInput, 42, output);
            });
            StringAssert.StartsWith("Identity Crisis:", e!.Message!);
        } {
            using var gpuWeight2 = device2.CreateBuffer(weight, GpuBufferUsage.Storage, "gpuWeight2"); 
            var e = Assert.Throws<InvalidOperationException>(() => {
                GpuPattern.ShadowMethod(gpuWeight2, input, 42, output);
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
            using var gpuWeight   = device.CreateBuffer(weight, GpuBufferUsage.Storage, "gpuWeight");
            using var gpuInput    = device.CreateBuffer(input,  GpuBufferUsage.Storage, "gpuInput");
            using var gpuOutput   = device.CreateBuffer(output, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "gpuOutput");
            
            GpuBuffer<float> result = null;
            for (int n = 0; n < 5; ++n) {
                result = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
                Console.WriteLine(HandleDiff.GetState());
            }
            device.Wait(result);
        }
        Console.WriteLine(HandleDiff.GetState());
    }
    
    [Test]
    public void Test_GPU_Adapter()
    {
        using var gpuWeight   = Device.CreateBuffer<float>(100, GpuBufferUsage.Storage, "test-buffer");
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