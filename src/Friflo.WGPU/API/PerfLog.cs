// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;

// ReSharper disable EmptyConstructor
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU;

public sealed class PerfLog
{
    public              long            MemoryAllocated { get; private set; }
    public              int             FrameCount      { get; private set; }
    public  readonly    StringBuilder   Builder         = new(256);
    private readonly    StreamWriter    writer          = new(Console.OpenStandardOutput(), new UTF8Encoding(false), 256); // false -> no BOM
    
    
    public PerfLog() { }

    public void Flush()
    {
        if (Builder.Length == 0) {
            return;
        }
        foreach (var chunk in Builder.GetChunks()) {
            writer.Write(chunk.Span);
        }
        writer.Flush();
        Builder.Clear();
    }
        
    public void Trace(int lapCount)
    {
        if (FrameCount == 0) {
            MemoryAllocated = GC.GetAllocatedBytesForCurrentThread();
        }
        if (FrameCount % lapCount == 0) {
            Builder.Append("frame: ");
            Builder.Append(FrameCount);
            Builder.AppendLine();
        }
        var cur = GC.GetAllocatedBytesForCurrentThread();
        if (cur != MemoryAllocated) {
            Builder.AppendLine($"frame: {FrameCount} - memory allocations: {cur - MemoryAllocated} ");
        }
        FrameCount++;
        Flush();
        MemoryAllocated = GC.GetAllocatedBytesForCurrentThread();
    }
}