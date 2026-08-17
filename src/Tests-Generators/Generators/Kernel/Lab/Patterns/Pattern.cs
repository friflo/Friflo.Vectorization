using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Friflo.Vectorization;
using Friflo.GPU;
using Friflo.GPU.Runtime;
using Friflo.Vectorization.Intrinsics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InconsistentNaming
// ReSharper disable PartialTypeWithSinglePart
namespace Kernel.Lab;

public static partial class Pattern
{
    // [Kernel, Vectorize]
    private static void MultiplyAdd(
        [Span]      float weight,
        [Span]      float input,
                    float bias,
        [Span] ref  float output)
    {
        output = weight * input + bias;
    }
    
    
    // Generated *Kernel method
    public static void MultiplyAddKernel(
      InBuffer<float>   weight,
      InBuffer<float>   input,
        float           bias,
        InOutBuffer<float>   output,
        ComputeMode     computeMode = ComputeMode.Device)
    {
        var buffers =
        GpuBuffers.Create(weight, nameof(weight), computeMode);
        buffers.Validate (input,  nameof(input));
        buffers.Validate (output, nameof(output));

        if (buffers.ComputeGPU) {
            switch (KernelFixture.TestBackend) {
                case TestBackend.WGPU:  WgpuPattern.MultiplyAdd_GPU(buffers, weight, input, bias, output);  return;
            //  case TestBackend.Silk:  SilkPattern.ShadowMethod_GPU(buffers, weight, input, bias, output); return;
            }
        }
        MultiplyAddVector_gen(weight.Span, input.Span, bias, output.Span, buffers.ComputeSIMD);
    }
    
    public static void MultiplyAddKernel_no_Vectorize (
      InBuffer<float>   weight,
      InBuffer<float>   input,
        float           bias,
        InOutBuffer<float>   output,
        ComputeMode     computeMode = ComputeMode.Device)
    {
        var buffers =
        GpuBuffers.Create(weight, nameof(weight), computeMode);
        buffers.Validate (input,  nameof(input));
        buffers.Validate (output, nameof(output));

        if (buffers.ComputeGPU) {
            WgpuPattern.MultiplyAdd_GPU(buffers, weight, input, bias, output);
            return;
        }
        _MultiplyAddVector_scalar(weight.Span, input.Span, bias, output.Span);
    }
    
    private static void _MultiplyAddVector_scalar(ReadOnlySpan<float> weight, ReadOnlySpan<float> input, float bias, Span<float> output)
    {
        int length = weight.Length;
        
        ref float weightRef = ref MemoryMarshal.GetReference(weight);
        ref float inputRef  = ref MemoryMarshal.GetReference(input);
        ref float outputRef = ref MemoryMarshal.GetReference(output);

        for (int i = 0; i < length; i++) {
            MultiplyAdd(Unsafe.Add(ref weightRef, i), Unsafe.Add(ref inputRef, i), bias, ref Unsafe.Add(ref outputRef, i));
        }
    }
    
    // generated AVX method
    /// <summary>Vector method generated for: <see cref="MultiplyAdd"/>.</summary>
    public static void MultiplyAddVector_gen(ReadOnlySpan<float> weight, ReadOnlySpan<float> input, float bias, Span<float> output, bool vectorized = true)
    {
        int count = weight.Length;
        int n = 0;
        if (vectorized) {
            if (Avx.IsSupported) {
                n = _MultiplyAdd_Avx(count, weight, input, bias, output);
            }
        }
        for (; n < count; n++) {
            MultiplyAdd(weight[n], input[n], bias, ref output[n]);
        }
    }

#region private members
    // [Layout: AoS-Vertical]  - lane-native speed
    [SkipLocalsInit]
    private static unsafe int _MultiplyAdd_Avx(int count,
        ReadOnlySpan<float> weight,
        ReadOnlySpan<float> input,
        float bias,
        Span<float> output)
    {
        int i = 0;
        count -= 32;
        if (i > count) {
            return 0;
        }
        if (weight.Length < count) VectorUtils.ThrowBufferTooSmall(nameof(weight));
        if (input.Length < count) VectorUtils.ThrowBufferTooSmall(nameof(input));
        if (output.Length < count) VectorUtils.ThrowBufferTooSmall(nameof(output));

        // --- Locals
        var bias_scalar = Vector256.Create(bias);

        fixed (float* weight_first = weight)
        fixed (float* input_first = input)
        fixed (float* output_first = output)
        {
            float* weight_ptr = weight_first;
            float* input_ptr = input_first;
            float* output_ptr = output_first;

            for (; i <= count; i += 32)
            {
                // --- 1. Load
                Vector256<float> weight_0 = Avx.LoadVector256(weight_ptr +  0);  // Single
                Vector256<float> weight_1 = Avx.LoadVector256(weight_ptr +  8);  // Single
                Vector256<float> weight_2 = Avx.LoadVector256(weight_ptr + 16);  // Single
                Vector256<float> weight_3 = Avx.LoadVector256(weight_ptr + 24);  // Single

                Vector256<float> input_0 = Avx.LoadVector256(input_ptr +  0);  // Single
                Vector256<float> input_1 = Avx.LoadVector256(input_ptr +  8);  // Single
                Vector256<float> input_2 = Avx.LoadVector256(input_ptr + 16);  // Single
                Vector256<float> input_3 = Avx.LoadVector256(input_ptr + 24);  // Single

                Vector256<float> output_0;  // Single
                Vector256<float> output_1;  // Single
                Vector256<float> output_2;  // Single
                Vector256<float> output_3;  // Single

                // --- 2. Compute
                // output = weight * input + bias;
                output_0 = Fma.MultiplyAdd(weight_0, input_0, bias_scalar);
                output_1 = Fma.MultiplyAdd(weight_1, input_1, bias_scalar);
                output_2 = Fma.MultiplyAdd(weight_2, input_2, bias_scalar);
                output_3 = Fma.MultiplyAdd(weight_3, input_3, bias_scalar);

                // --- 3. Store
                Avx.Store(output_ptr +  0, output_0);
                Avx.Store(output_ptr +  8, output_1);
                Avx.Store(output_ptr + 16, output_2);
                Avx.Store(output_ptr + 24, output_3);

                weight_ptr += 32;
                input_ptr += 32;
                output_ptr += 32;
            }
        }
        return i;
    }
#endregion

}