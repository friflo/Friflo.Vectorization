// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using NUnit.Framework;

// ReSharper disable CheckNamespace
namespace Kernel.Generators;


public partial class HelloWorld
{
    [Kernel, Vectorize] [OmitHash]
    private static void Add([Span] float a, [Span] float b, [Span] ref float c) {
        c = a + b;
    }
    
    [Test]
    public static void Test_GPU_HelloWorld()
    {
        // using var instance    = CpuInstance.CreateInstance();
        // using var adapter     = instance.CreateAdapter(GpuBackendType.SIMD);
        using var instance    = WgpuInstance.CreateInstance(new InstanceExtras());
        using var adapter     = instance.RequestAdapter(default, null);
        using var device      = adapter.CreateDevice("test");
        
        using var a = device.CreateBuffer(1024, 1f, "a", BufferProfile.StaticIn);
        using var b = device.CreateBuffer(1024, 2f, "b", BufferProfile.StaticIn);
        using var c = device.CreateBuffer(1024, 0f, "c", BufferProfile.InOut);
        
        using var context = device.BeginContext();

        AddKernel(a.In, b.In, c.InOut.StageRead());
        
        context.Queue.ReadBuffers();
        
        Console.WriteLine($"✓ SUCCESS: c[0] = {c.InOut.Span[0]} (Expected: 3.0)");
    }
}
