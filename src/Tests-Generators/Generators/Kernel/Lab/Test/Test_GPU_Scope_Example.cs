using System;
using Friflo.Vectorization.GPU;

// ReSharper disable InconsistentNaming
namespace Kernel.Lab;

public static class Test_GPU_Scope_Example
{
    private static void ReadOnlyAccess(IReadOnlyGpuBuffer<float> gpuBuffer)
    {
        var view = gpuBuffer.In(); // buffer data view is immutable
    } 
    
    
    private static void Upload(IScopedWriteBuffer<float> gpuBuffer)
    {
        // update buffer data
        using var writer = gpuBuffer.GetWriter();
        
        // Safe to access Span here. Data is uploaded after leaving the scope.
        writer.Span[0] = 42;
        writer.Span[1] = 1337;
    } // <-- Upload

    
    private static void Download(IScopedReadBuffer<float> gpuBuffer)
    {
        using var reader = gpuBuffer.GetReader();
        
        // At this point GPU data is already downloaded
        ReadOnlySpan<float> results = reader.Span;
        for (int i = 0; i < results.Length; i++) {
            Console.WriteLine($"Ergebnis {i}: {results[i]}");
        }
    }
}