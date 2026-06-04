using Friflo.Vectorization;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;

// using var instance    = CpuInstance.CreateInstance();
// using var adapter     = instance.CreateAdapter(GpuBackendType.SIMD);
using var instance    = WgpuInstance.CreateInstance(new InstanceExtras());
using var adapter     = instance.RequestAdapter(default, null);
using var device      = adapter.CreateDevice("test");

using var a = device.CreateBuffer(1024, 1f, "a", BufferProfile.StaticIn);
using var b = device.CreateBuffer(1024, 2f, "b", BufferProfile.StaticIn);
using var c = device.CreateBuffer(1024, 0f, "c", BufferProfile.InOut);

using var context = device.BeginContext();
context.PassBatching = PassBatching.HazardDriven;

for (int n = 1; n <= 1_000_000; n++) {
    if (n % 10_000 == 0) { Console.WriteLine($"iteration: {n}"); }
    
    HelloWorld.AddKernel(a.In, b.In, c.InOut);
    if (n  % 100 == 0) {
        context.Queue.ReadBuffers();
    }
    // if (n == 1_000_000) { n = 0; }
}

Console.WriteLine($"✓ SUCCESS: c[0] = {c.InOut.Span[0]} (Expected: 3.0)");

public static partial class HelloWorld
{
    [Kernel] [OmitHash]
    private static void Add([Span] float a, [Span] float b, [Span] ref float c) {
        c = a + b;
    }
}
