using System;
using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;

namespace Tests.GPU;

public  static partial class GpuPattern
{
    [Vectorize]
    public static void MultiplyAdd(
        [Span]      float weight,
        [Span]      float input,
                    float bias,
        [Span] ref  float output)
    {
        output = weight * input + bias;
    }
    // generated shadow Method
    public static GpuBuffer<float> ShadowMethod(
        Buffer<float>   weight,
        Buffer<float>   input,
        float           bias,
        Buffer<float>   output,
        ExeType         exe = ExeType.GPU)
    {
        var buffers = new GpuBuffers();
        buffers.Validate(weight, nameof(weight));
        buffers.Validate(input,  nameof(input));
        buffers.Validate(output, nameof(output));
        
        if (exe == ExeType.GPU) {
            switch (GpuTestBase.Backend) {
                case TestBackend.WebGPU:    return WebGPUPattern.ShadowMethod_GPU(buffers, weight.gpuBuffer, input.gpuBuffer, bias, output.gpuBuffer);
                case TestBackend.Silk:      return SilkPattern.  ShadowMethod_GPU(buffers, weight.gpuBuffer, input.gpuBuffer, bias, output.gpuBuffer);
            }
        }
        MultiplyAddVector(weight.span, input.span, bias, output.span);
        return output.gpuBuffer;
    }
    
    // generated AVX method
    private static void ShadowMethod_AVX(ReadOnlySpan<float> weight, ReadOnlySpan<float> input, float bias, Span<float> output, bool vectorized = true) {
        // ...
    }
}