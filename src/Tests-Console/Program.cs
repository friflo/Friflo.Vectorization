using System.Diagnostics;
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

var size = 1024;

using var a = device.CreateBuffer(size, 1f, "a", BufferProfile.StaticIn);
using var b = device.CreateBuffer(size, 2f, "b", BufferProfile.StaticIn);
using var c = device.CreateBuffer(size, 0f, "c", BufferProfile.InOut);

using var context = device.BeginContext();
context.PassBatching = PassBatching.HazardDriven;

var stopwatch   = Stopwatch.StartNew();
var iterations  = 1_000_000;

for (int n = 1; n <= 1000_000; n++) {
    if (n % 10_000 == 0) { Console.WriteLine($"iteration: {n}  c[0] = {c.InOut.Span[0]}"); }
    
    HelloWorld.AddKernel(a.In, b.In, c.InOut);
    
    if (n  % 100 == 0) { context.Queue.ReadBuffers(); }
    if (n == 1_000_000) { n = 0; }
}

Console.WriteLine($"mode: {device.DefaultComputeMode}  iterations: {iterations}  time: {stopwatch.ElapsedMilliseconds} ms.  c[0] = {c.InOut.Span[0]} (Expected: 3.0)");

public static partial class HelloWorld
{
    [Kernel, Vectorize] [OmitHash]
    private static void Add([Span] float a, [Span] float b, [Span] ref float c) {
        c = a + b;
    }
}
