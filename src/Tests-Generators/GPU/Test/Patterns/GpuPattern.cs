using Friflo.Vectorization.GPU;

namespace Tests.GPU;

public static class GpuPattern
{
    // generated shadow Method
    public static GpuBuffer<float> ShadowMethod(
        Buffer<float>   weight,
        Buffer<float>   input,
        float           bias,
        ExeType         exe,
        Buffer<float>   output = default)
    {
        if (exe == ExeType.GPU) {
            if (GpuTestGlobal.UseSilk) {
                return SilkPattern.ShadowMethod_GPU(weight, input, bias, output);
            }
            return WebGPUPattern.ShadowMethod_GPU(weight, input, bias, output);
        }
        // Scalar / SIMD
        ShadowMethod_AVX(weight, input, bias, output);
        return null;
    }
    
    // generated AVX method
    private static void ShadowMethod_AVX(Buffer<float> weight, Buffer<float> input, float bias, Buffer<float> output) {
        // ...
    }
}