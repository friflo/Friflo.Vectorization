using System;
using System.Threading.Tasks;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Tests.Utils;

// ReSharper disable InconsistentNaming
namespace Tests.GPU;

[TestFixture(TestBackend.SIMD,      TestName = "SIMD")]
[TestFixture(TestBackend.WebGPU,    TestName = "WebGPU")]
public class TestCompute : GpuTestBase
{
    public TestCompute(TestBackend backend) : base(backend) { }
    
    // ------------------------ generated code: end
    private static void UseSpan<T>(Span<T> span) { }
    
    public async Task ExampleCompute()
    {
        var weight  = new Span<float> ([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var input   = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var output  = new float[9];
        
        UseSpan(weight);
        
        var result1 = GpuPattern.ShadowMethod(weight, input, 42, output, ExeType.SIMD);
        // result1 - no Wait() on result1. Nothing will happen - user is surprised :)
        
    //  UseSpan(weight); // compiler error
        
        using var device    = Adapter.CreateDevice("ExampleCompute");
        var gpuWeight = device.CreateBuffer<float>(100, GpuBufferUsage.None, "weight");
        var gpuInput  = device.CreateBuffer<float>(100, GpuBufferUsage.None, "input");
        var output2   = device.CreateBuffer<float>(100, GpuBufferUsage.None, "output2");
        var result2 = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, output2, ExeType.SIMD);
        device.Wait(result2);
    }
    
    public class ModelLayer {
        public GpuBuffer<float>    weight;
        public GpuBuffer<float>    input;
        public GpuBuffer<float>    output;
    }
    
    public static void RunInference(ModelLayer[] layers, GpuDevice device)
    {
        // Fire Layer 1 to 50
        GpuBuffer<float> result = null;
        foreach (var layer in layers) {
            result = GpuPattern.ShadowMethod(layer.weight, layer.input, 42, layer.output);
        }
        // Wait only on lastTask. Very efficient. GpuTask works intern with DevicePoll()
        device.Wait(result);
    }
    

    // --- compact examples of some generated shadow method stubs
    public static GpuBuffer<float> ComputeLayer1(Buffer<byte> weight, Buffer<float> input, ExeType exe) { return null; }
    public static GpuBuffer<float> ComputeLayer2(Buffer<float> input, ExeType exe) { return null; }
    
    public static GpuBuffer<byte> InitWeights(GpuDevice device) {
        return null;
    }
    

    public void DependencyFlow(Buffer<float> input)
    {
        using var device    = Adapter.CreateDevice("DependencyFlow");
        var weight = InitWeights(device);
        var a = ComputeLayer1(weight, input, ExeType.GPU);
    //  firstValue = a[0];                              // TODO indexer must device.Wait(this) - than returns firstValue
        var b = ComputeLayer2(a, ExeType.GPU);
        device.Wait(b);
    }
    
    // Force one time allocations caused by JIT
    private void WarmUpDevice()
    {
        using var device    = Adapter.CreateDevice("WarmUpDevice");
        using var gpuWeight   = device.CreateBuffer<float>(64, GpuBufferUsage.Storage, "weight");
        using var gpuInput    = device.CreateBuffer<float>(64,  GpuBufferUsage.Storage, "input");
        using var gpuOutput   = device.CreateBuffer<float>(64, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "output");
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
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
        using var gpuWeight   = device.CreateBuffer(weight, GpuBufferUsage.Storage, "weight");
        using var gpuInput    = device.CreateBuffer(input,  GpuBufferUsage.Storage, "input");
        using var gpuOutput   = device.CreateBuffer(output, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "output");
        
        var start1 = Mem.GetAllocatedBytes();
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
        Mem.AssertNoAlloc(start1);
        
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);

        var start3 = Mem.GetAllocatedBytes();
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
        Mem.AssertNoAlloc(start3);

        using var result = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
        
        device.Wait(result);
        
        gpuOutput.Download(result, output);
        Assert.AreEqual(42, output[0]);
    }
    
    [Test]
    public void Test_GPU_BufferBindGroupCaching()
    {
        if (Backend == TestBackend.SIMD) return;
        
        var device    = Device;

        var weight  = new float[65]; // no alignment
        var input   = new float[65];
        var output  = new float[65];
        var output2 = new float[65];
        var output3 = new float[65];
        for (int n = 0; n < 64; ++n) { weight[n] = n; input[n]  = n + 1000; }
        using var gpuWeight   = device.CreateBuffer(weight,  GpuBufferUsage.Storage, "weight");
        using var gpuInput    = device.CreateBuffer(input,   GpuBufferUsage.Storage, "input");
        using var gpuOutput   = device.CreateBuffer(output,  GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "output");
        using var gpuOutput2  = device.CreateBuffer(output2, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "output2");
        using var gpuOutput3  = device.CreateBuffer(output3, GpuBufferUsage.Storage | GpuBufferUsage.CopySrc, "output3");
        
        Assert.AreEqual(0, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(0, HandleDiff.BindGroups.Diff);
        
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, gpuOutput);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(2, HandleDiff.BindGroups.Diff);
        
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 43, gpuOutput2);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(4, HandleDiff.BindGroups.Diff);
        
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 44, gpuOutput);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(5, HandleDiff.BindGroups.Diff); // cache hit: gpuOutput
        
        GpuPattern.ShadowMethod(gpuWeight, gpuInput, 45, gpuOutput3);
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(7, HandleDiff.BindGroups.Diff);
        
        device.Wait(gpuOutput);
        
        gpuOutput.Download(gpuOutput, output);
        Assert.AreEqual(44, output[0]);
    }
    
}