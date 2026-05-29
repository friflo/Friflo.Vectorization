using System;
using System.Threading.Tasks;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.Utils;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;

public class TestCompute : KernelBase
{
    // ------------------------ generated code: end
    private static void UseSpan<T>(Span<T> span) { }
    
    public async Task ExampleCompute()
    {
        var weight  = new Span<float> ([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var input   = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var output  = new float[9];
        
        UseSpan(weight);
        
        GpuPattern.ShadowMethod(weight, input, 42, output, ComputeMode.SIMD);
        // result1 - no Wait() on result1. Nothing will happen - user is surprised :)
        
    //  UseSpan(weight); // compiler error
        
        using var device    = Adapter.CreateDevice("ExampleCompute");
        var gpuWeight = device.CreateBuffer<float>(100, "weight",   BufferProfile.StaticIn);
        var gpuInput  = device.CreateBuffer<float>(100, "input",    BufferProfile.StaticIn);
        var output2   = device.CreateBuffer<float>(100, "output2",  BufferProfile.StaticIn);
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, output2.InOut, ComputeMode.SIMD);
        
        device.Download();
    }
    
    public class ModelLayer {
        public GpuBuffer<float>    weight;
        public GpuBuffer<float>    input;
        public GpuBuffer<float>    output;
    }
    
    public static void RunInference(ModelLayer[] layers, GpuDevice device)
    {
        // Fire Layer 1 to 50

        foreach (var layer in layers) {
            GpuPattern.ShadowMethod(layer.weight.In, layer.input.In, 42, layer.output.InOut);
        }
        // Wait only on lastTask. Very efficient. SilkTask works intern with DevicePoll()
        device.Download();
    }
    

    // --- compact examples of some generated shadow method stubs
    public static GpuBuffer<float> ComputeLayer1(Buffer<byte> weight, Buffer<float> input, ComputeMode compute) { return null; }
    public static GpuBuffer<float> ComputeLayer2(Buffer<float> input, ComputeMode compute) { return null; }
    
    public static GpuBuffer<byte> InitWeights(GpuDevice device) {
        return null;
    }
    

    public void DependencyFlow(Buffer<float> input)
    {
        using var device    = Adapter.CreateDevice("DependencyFlow");
        var weight = InitWeights(device);
        var a = ComputeLayer1(weight.InOut, input, ComputeMode.GPU);
    //  firstValue = a[0];                              // TODO indexer must device.Wait(this) - than returns firstValue
        var b = ComputeLayer2(a.InOut, ComputeMode.GPU);
        
        device.Download();
    }
    
    // Force one time allocations caused by JIT
    private void WarmUpDevice()
    {
        using var device    = Adapter.CreateDevice("WarmUpDevice");
        using var gpuWeight   = device.CreateBuffer<float>(64,  "weight",   BufferProfile.StaticIn);
        using var gpuInput    = device.CreateBuffer<float>(64,  "input",    BufferProfile.StaticIn);
        using var gpuOutput   = device.CreateBuffer<float>(64,  "output",   BufferProfile.InOut);
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
    }
    
    [Test]
    public void TestExampleGPU()
    {
        WarmUpDevice();
        var device    = Device;

        var weight  = new float[65]; // no alignment
        var input   = new float[65];
        var output  = new float[65];
        for (int n = 0; n < 64; ++n) {
            weight[n] = n;
            input[n]  = n + 1000;
        }
        using var gpuWeight   = device.CreateBuffer(weight, "weight", BufferProfile.StaticIn);
        using var gpuInput    = device.CreateBuffer(input,  "input",  BufferProfile.StaticIn);
        using var gpuOutput   = device.CreateBuffer(output, "output", BufferProfile.InOut);
        
        // var start1 = Mem.GetAllocatedBytes();                        // TODO should add allocation check for first call
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        // Mem.AssertNoAlloc(start1);
        
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);

        var start3 = Mem.GetAllocatedBytes();
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        Mem.AssertNoAlloc(start3);

        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        
        // device.Wait(gpuOutput);
        // gpuOutput.Download(gpuOutput, output);
        device.Download();
        
        Assert.AreEqual(42, output[0]);
    }
    
    [Test]
    public void Test_GPU_BufferBindGroupCaching()
    {
        if (KernelFixture.TestBackend == TestBackend.Scalar || KernelFixture.TestBackend == TestBackend.SIMD)
            return;
        
        var device    = Device;

        var weight  = new float[65]; // no alignment
        var input   = new float[65];
        var output  = new float[65];
        var output2 = new float[65];
        var output3 = new float[65];
        for (int n = 0; n < 64; ++n) { weight[n] = n; input[n]  = n + 1000; }
        using var gpuWeight   = device.CreateBuffer(weight,  "weight",  BufferProfile.StaticIn);
        using var gpuInput    = device.CreateReadOnlyBuffer(input, "input");
        using var gpuOutput   = device.CreateBuffer(output,  "output",  BufferProfile.InOut);
        using var gpuOutput2  = device.CreateBuffer(output2, "output2", BufferProfile.InOut);
        using var gpuOutput3  = device.CreateBuffer(output3, "output3", BufferProfile.InOut);
        
        Assert.AreEqual(0, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(0, HandleDiff.BindGroups.Diff);
        
        var context = device.PipelineContext;
        context.EnablePassBatching = false; // uniform bind group is always released (destroyed)
        
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 42, gpuOutput.InOut);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(1, HandleDiff.BindGroups.Diff);
        
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 43, gpuOutput2.InOut);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(2, HandleDiff.BindGroups.Diff);
        
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 44, gpuOutput.InOut);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(2, HandleDiff.BindGroups.Diff); // cache hit: gpuOutput
        
        GpuPattern.ShadowMethod(gpuWeight.In, gpuInput.In, 45, gpuOutput3.InOut);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(2, HandleDiff.BindGroups.Diff);
        
        // device.Wait(gpuOutput);
        // gpuOutput.Download(gpuOutput, output);
        
        device.Download();
        
        Assert.AreEqual(44, output[0]);
    }
    
}