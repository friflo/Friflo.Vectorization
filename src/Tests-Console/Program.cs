using System.Diagnostics;
using Friflo.Vectorization;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using TestConsole;

// ReSharper disable HeuristicUnreachableCode
#pragma warning disable CS0162 // Unreachable code detected

Console.OutputEncoding = System.Text.Encoding.UTF8; // support UTF-8 chars like 🙂



SdlWindow.Run("ImGuiRenderer",  1280, 720, wgpu => new ImGuiRenderer(wgpu));
SdlWindow.Run("ImRenderer",     1280, 720, wgpu => new ImRenderer(wgpu));

SdlWindow.Run("Particles",      1280, 720, wgpu => new Shaders.Particles.Renderer(wgpu));
SdlWindow.Run("RenderTest",     1280, 720, wgpu => new Shaders.RenderTest.Renderer(wgpu));
SdlWindow.Run("ShadowMapping",  1280, 720, wgpu => new Shaders.ShadowMapping.Renderer(wgpu));
SdlWindow.Run("InstancedCube",  1280, 720, wgpu => new Shaders.InstancedCube.Renderer(wgpu));
SdlWindow.Run("TwoCubes",       1280, 720, wgpu => new Shaders.TwoCubes.Renderer(wgpu));
SdlWindow.Run("TexturedCube",   1280, 720, wgpu => new Shaders.TexturedCube.Renderer(wgpu));
return SdlWindow.Run("ConfigTest",     1280, 720, wgpu => new Shaders.RenderTest.ConfigTest(wgpu));


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
