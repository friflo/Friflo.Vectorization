// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public sealed class PerfLog
{
    private long                            memoryAllocated;
    private int                             frameCount;
    private static readonly StringBuilder   Builder = new(256);
    private static readonly StreamWriter    Writer  = new(Console.OpenStandardOutput(), Encoding.UTF8, 256);

    public void Flush()
    {
        foreach (var chunk in Builder.GetChunks()) {
            Writer.Write(chunk.Span);
        }
        Writer.Flush();
        Builder.Clear();
    }
        
    public void Trace(int lapCount)
    {
        if (++frameCount % lapCount == 0) {
            Builder.Append("frame: ");
            Builder.Append(frameCount);
            Builder.AppendLine();
            Flush();
        }
        var cur = GC.GetAllocatedBytesForCurrentThread();
        if (cur != memoryAllocated) {
            Builder.AppendLine($"memory allocations: {cur - memoryAllocated} ");
            Flush();
        }
        memoryAllocated = GC.GetAllocatedBytesForCurrentThread();
    }
}