using System.Diagnostics;
using Friflo.Vectorization;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using TestConsole;

Console.OutputEncoding = System.Text.Encoding.UTF8; // support UTF-8 chars like 🙂

SdlWindow.Main("InstancedCube",  1280, 720, wgpu => new InstancedCube(wgpu));
SdlWindow.Main("TwoCubes",       1280, 720, wgpu => new TwoCubes(wgpu));
SdlWindow.Main("TexturedCube",   1280, 720, wgpu => new TexturedCube(wgpu));
SdlWindow.Main("ConfigTest",     1280, 720, wgpu => new ConfigTest(wgpu));
return SdlWindow.Main("RenderTest",     1280, 720, wgpu => new RenderTest(wgpu));

// using var instance    = CpuInstance.CreateInstance();
// using var adapter     = instance.CreateAdapter(GpuBackendType.SIMD);
using var instance    = WgpuInstance.CreateInstance();
using var adapter     = instance.RequestAdapter(default); // specific backend: new GpuRequestAdapterOptions { backendType = BackendType.D3D12 }
using var device      = adapter.CreateDevice("test");

var size = 1024;

using var a = device.CreateBuffer(size, 1f, "a", BufferProfile.StaticIn);
using var b = device.CreateBuffer(size, 2f, "b", BufferProfile.StaticIn);
using var c = device.CreateBuffer(size, 0f, "c", BufferProfile.InOut);

using var context = device.BeginContext();
context.PassBatching = PassBatching.HazardDriven;

var stopwatch   = Stopwatch.StartNew();
var checkPoint 	= stopwatch.ElapsedMilliseconds;
var iterations  = 1_000_000;

for (int n = 1; n <= iterations; n++) {
    if (n % 50_000 == 0) { Console.WriteLine($"iteration: {n}  c[0] = {c.InOut().Span[0]}"); }
    
    HelloWorld.AddKernel(a.In(), b.In(), c.InOut().Read());
    
    if (n  % 200 == 0) { context.Queue.ReadBuffers(); }
    if (n == iterations) { n = 0; Console.WriteLine($"----------------------- checkpoint: {stopwatch.ElapsedMilliseconds - checkPoint} ms"); checkPoint = stopwatch.ElapsedMilliseconds; }
}

Console.WriteLine($"mode: {device.DefaultComputeMode}  iterations: {iterations}  time: {stopwatch.ElapsedMilliseconds} ms.  c[0] = {c.InOut().Span[0]} (Expected: 3.0)");

public static partial class HelloWorld
{
    [Kernel] [OmitHash]
    private static void Add([Span] float a, [Span] float b, [Span] ref float c) {
        c = a + b;
    }
}
