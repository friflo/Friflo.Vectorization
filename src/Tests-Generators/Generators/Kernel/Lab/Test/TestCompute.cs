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
        
        Pattern.MultiplyAddKernel(weight, input, 42, output, ComputeMode.SIMD);
        // result1 - no Wait() on result1. Nothing will happen - user is surprised :)
        
    //  UseSpan(weight); // compiler error
        
        using var device    = Adapter.CreateDevice("ExampleCompute");
        var gpuWeight = device.CreateBuffer(100, 1f, "weight",   BufferProfile.StaticIn);
        var gpuInput  = device.CreateBuffer(100, 2f, "input",    BufferProfile.StaticIn);
        var output2   = device.CreateBuffer(100, 3f, "output2",  BufferProfile.StaticIn);
        
        using var context = device.BeginContext();
        
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 42, output2.InOut().Read(), ComputeMode.SIMD);
        
        context.Queue.ReadBuffers();
    }
    
    public class ModelLayer {
        public GpuBuffer<float>    weight;
        public GpuBuffer<float>    input;
        public GpuBuffer<float>    output;
    }
    
    public static void RunInference(ModelLayer[] layers, GpuDevice device)
    {
        // Fire Layer 1 to 50
        using var context = device.BeginContext();

        foreach (var layer in layers) {
            Pattern.MultiplyAddKernel(layer.weight.In(), layer.input.In(), 42, layer.output.InOut().Read());
        }
        // Wait only on lastTask. Very efficient. SilkTask works intern with DevicePoll()
        context.Queue.ReadBuffers();
    }
    

    // --- compact examples of some generated shadow method stubs
    public static GpuBuffer<float> ComputeLayer1(InOutBuffer<byte> weight, InOutBuffer<float> input, ComputeMode compute) { return null; }
    public static GpuBuffer<float> ComputeLayer2(InOutBuffer<float> input, ComputeMode compute) { return null; }
    
    public static GpuBuffer<byte> InitWeights(GpuDevice device) {
        return null;
    }
    

    public void DependencyFlow(InOutBuffer<float> input)
    {
        using var device    = Adapter.CreateDevice("DependencyFlow");
        var weight = InitWeights(device);
        var a = ComputeLayer1(weight.InOut().Read(), input, ComputeMode.GPU);
    //  firstValue = a[0];                              // TODO indexer must device.Wait(this) - than returns firstValue
        var b = ComputeLayer2(a.InOut().Read(), ComputeMode.GPU);
        
        // device.Download();
    }
    
    // Force one time allocations caused by JIT
    private void WarmUpDevice()
    {
        using var device    = Adapter.CreateDevice("WarmUpDevice");
        using var gpuWeight   = device.CreateBuffer(64, 1f,  "weight",   BufferProfile.StaticIn);
        using var gpuInput    = device.CreateBuffer(64, 2f,  "input",    BufferProfile.StaticIn);
        using var gpuOutput   = device.CreateBuffer(64, 3f,  "output",   BufferProfile.InOut);

        using var context = device.BeginContext();
        
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 42, gpuOutput.InOut().Read());
        
        context.Queue.ReadBuffers(); 						// TODO add test when ReadBuffers() is not called
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
        
        using var context = device.BeginContext();
        
        // var start1 = Mem.GetAllocatedBytes();                        // TODO should add allocation check for first call
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 42, gpuOutput.InOut().Read());
        // Mem.AssertNoAlloc(start1);
        
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 42, gpuOutput.InOut().Read());

        var start3 = Mem.GetAllocatedBytes();
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 42, gpuOutput.InOut().Read());
        Mem.AssertNoAlloc(start3);

        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 42, gpuOutput.InOut().Read());
        
        // device.Wait(gpuOutput);
        // gpuOutput.Download(gpuOutput, output);
        context.Queue.ReadBuffers();
        
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
        using var gpuWeight   = device.CreateBuffer(weight,        "weight",  BufferProfile.StaticIn);
        using var gpuInput    = device.CreateReadOnlyBuffer(input, "input");
        using var gpuOutput   = device.CreateBuffer(output,        "output",  BufferProfile.InOut);
        using var gpuOutput2  = device.CreateBuffer(output2,       "output2", BufferProfile.InOut);
        using var gpuOutput3  = device.CreateBuffer(output3,       "output3", BufferProfile.InOut);
        
        Assert.AreEqual(0, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(0, HandleDiff.BindGroups.Diff);
        
        using var context = device.BeginContext();
        context.PassBatching = PassBatching.None; // uniform bind group is always released (destroyed)
        
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 42, gpuOutput.InOut().Read());
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(2, HandleDiff.BindGroups.Diff);
        
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 43, gpuOutput2.InOut().Read());
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(3, HandleDiff.BindGroups.Diff);
        
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 44, gpuOutput.InOut().Read());
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(3, HandleDiff.BindGroups.Diff);
        
        Pattern.MultiplyAddKernel(gpuWeight.In(), gpuInput.In(), 45, gpuOutput3.InOut().Read());
        Assert.AreEqual(2, HandleDiff.BindGroupLayouts.Diff);
        Assert.AreEqual(4, HandleDiff.BindGroups.Diff);
        
        context.Queue.ReadBuffers();
        
        Assert.AreEqual(44, output[0]);
    }
    
}