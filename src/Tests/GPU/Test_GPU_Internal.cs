

using System;
using System.Diagnostics;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Tests.GPU;

public static class Test_GPU_Internal
{
    [Test]
    public static void Test_GPU_Internal_DetectHazard()
    {
        using var instance    = WgpuInstance.CreateInstance(new InstanceExtras());
        using var adapter     = instance.RequestAdapter(default, null);
        using var device      = adapter.CreateDevice("test");
        
        using var buffer1 = device.CreateBuffer<float>(10, "buffer1", BufferProfile.StaticIn);
        using var buffer2 = device.CreateBuffer<float>(10, "buffer2", BufferProfile.StaticIn);
        using var buffer3 = device.CreateBuffer<float>(10, "buffer3", BufferProfile.StaticIn);
        
        const int repeat = 10;  //  10_000_000
        
        var stopWatch = Stopwatch.StartNew();
        for (int n = 0; n < repeat; n++) {
            TestKernel(buffer1.In, buffer2.In, buffer3.InOut);
        }
        Console.WriteLine($"DetectHazard - repeat: {repeat}   time: {stopWatch.ElapsedMilliseconds} ms");
    }
    
    private static void TestKernel(
      InBuffer<float>   buffer1,
      InBuffer<float>   buffer2,
        Buffer<float>   buffer3,
        ComputeMode     computeMode = ComputeMode.Device)
    {
        var buffers =
        GpuBuffers.Create(buffer1, nameof(buffer1), computeMode);
        buffers.Validate (buffer2, nameof(buffer2));
        buffers.Validate (buffer3, nameof(buffer3));
        
        var device      = (WgpuDevice)buffers.device;
        var recorder    = device.Recorder;
        recorder.Init(123);
        
        // Lock-free GPU kernels with deferred, on-the-fly hazard-driven pass batching
        var buffer1_    = recorder.RequireRead     (buffer1);
        var buffer2_    = recorder.RequireRead     (buffer2);
        var buffer3_    = recorder.RequireReadWrite(buffer3);
    }
}
