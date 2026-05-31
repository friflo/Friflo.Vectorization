// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.Vectorization;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using NUnit.Framework;

// ReSharper disable CheckNamespace
namespace Kernel.Generators;


public partial class HelloWorld : KernelBase
{
    [Kernel] [OmitHash]
    private static void Add([Span] float a, [Span] float b, [Span] ref float c) {
        c = a + b;
    }
    
    [Test]
    public static void Test_GPU_HelloWorld()
    {
        using var instance    = WgpuInstance.CreateInstance(new InstanceExtras());
        using var adapter     = instance.RequestAdapter(default, null);
        using var device      = adapter.CreateDevice("test");
        
        using var a = device.CreateBuffer<float>(1024, "a", BufferProfile.StaticIn);
        using var b = device.CreateBuffer<float>(1024, "b", BufferProfile.StaticIn);
        using var c = device.CreateBuffer<float>(1024, "c", BufferProfile.InOut);
        
        using var context = device.BeginContext();

        AddKernel(a.In, b.In, c.InOut);
    }
}
