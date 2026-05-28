// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public struct PipelineRecord
{
    public string   KernelName => KernelRegistry.GetKernelName(KernelId);
    public int      KernelId;
    public int      Calls;
    public int      Passes;

    public override string ToString() => $"'{KernelName}'  calls: {Calls}  passes: {Passes}";
}

public class PipelineContext
{
    public    virtual   bool                            EnablePassBatching { get; set; }
    public    virtual   bool                            EnableDiagnostics  { get; set; }
    public              ReadOnlySpan<PipelineRecord>    Records         => GetRecords();
    protected virtual   ReadOnlySpan<PipelineRecord>    GetRecords()    => default;
}

public static class KernelRegistry
{
    private static          string[]    kernelNames = new string[20];
    private static readonly object      mutex = new();
    private static          int         nextId;
    
    internal static string GetKernelName(int slot) => kernelNames[slot];
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int NewKernelId(string kernelName)
    {
        lock (mutex)
        {
            var newId = ++nextId;
            if (newId >= kernelNames.Length) {
                var newNames = new string[2 * kernelNames.Length];
                Array.Copy(kernelNames, newNames, kernelNames.Length);
                newNames[newId] = kernelName;
                kernelNames = newNames;
            } else {
                kernelNames[newId] = kernelName;
            }
            return newId;
        }
    }
}

