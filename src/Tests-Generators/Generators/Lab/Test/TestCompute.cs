using System;
using System.Threading.Tasks;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Lab;

public static class TestCompute
{

    // ------------------------ generated code: end

    
    
    private static void UseSpan<T>(Span<T> span) { }
    
    public static async Task ExampleCompute()
    {
        var weight  = new Span<float> ([1, 2, 3, 4, 5, 6, 7, 8, 9]);
        var input   = new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var output  = new float[9];
        
        UseSpan(weight);
        
        var result1 = GpuPattern.ShadowMethod(weight, input, 42, ExeType.SIMD, output);
        // result1 - no Wait() on result1. Nothing will happen - user is surprised :)
        
    //  UseSpan(weight); // compiler error
        
        using var gpuContext = GpuContext.Create();
        var gpuWeight = new GpuBuffer<float>(gpuContext, 100, BufferUsage.None);
        var gpuInput  = new GpuBuffer<float>(gpuContext, 100, BufferUsage.None);
        var output2   = new GpuBuffer<float>(gpuContext, 100, BufferUsage.None);
        var result2 = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.SIMD, output2);
        gpuContext.Wait(result2);
    }
    
    public class ModelLayer {
        public GpuBuffer<float>    weight;
        public GpuBuffer<float>    input;
        public GpuBuffer<float>    output;
    }
    
    public static void RunInference(ModelLayer[] layers, GpuContext ctx)
    {
        // Fire Layer 1 to 50
        GpuBuffer<float> result = null;
        foreach (var layer in layers) {
            result = GpuPattern.ShadowMethod(layer.weight, layer.input, 42, ExeType.GPU, layer.output);
        }
        // Wait only on lastTask. Very efficient. GpuTask works intern with DevicePoll()
        ctx.Wait(result);
    }
    

    // --- compact examples of some generated shadow method stubs
    public static GpuBuffer<float> ComputeLayer1(Buffer<byte> weight, Buffer<float> input, ExeType exe) { return null; }
    public static GpuBuffer<float> ComputeLayer2(Buffer<float> input, ExeType exe) { return null; }
    
    public static GpuBuffer<byte> InitWeights(GpuContext context) {
        return null;
    }
    

    public static void DependencyFlow(Buffer<float> input)
    {
        using var context = GpuContext.Create();
        var weight = InitWeights(context);
        var a = ComputeLayer1(weight, input, ExeType.GPU);
    //  firstValue = a[0];                              // TODO indexer must ctx.Wait(this) - than returns firstValue
        var b = ComputeLayer2(a, ExeType.GPU);
        context.Wait(b);
    }
    
    [Test]
    public static void TestExampleGPU()
    {
        using var context = GpuContext.Create();
        var weight  = new float[64]; // no alignment
        var input   = new float[64];
        var output  = new float[64];
        for (int n = 0; n < 64; ++n) {
            weight[n] = n;
            input[n]  = n + 1000;
        }
        using var gpuWeight   = new GpuBuffer<float>(context, weight, BufferUsage.Storage);
        using var gpuInput    = new GpuBuffer<float>(context, input,  BufferUsage.Storage);
        using var gpuOutput   = new GpuBuffer<float>(context, output, BufferUsage.Storage | BufferUsage.CopySrc);

        for (int n = 0; n < 2; ++n) {
            GpuBuffer<float> temp = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput);    
        }
        using var result = GpuPattern.ShadowMethod(gpuWeight, gpuInput, 42, ExeType.GPU, gpuOutput);
        
        context.Wait(result);
        
        gpuOutput.Download(result, output);
        Console.WriteLine($"output[0] {output[0]}");
    }
    
}