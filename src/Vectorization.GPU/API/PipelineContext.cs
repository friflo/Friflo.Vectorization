// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public enum PipelineRecordType : byte
{
    Kernel,
    KernelSubmit,
    BatchSubmit
}

public struct PipelineRecord
{
    public  PipelineRecordType  RecordType;
    public  string              KernelName => KernelRegistry.GetKernelName(KernelId);
    public  int                 KernelId;
    public  int                 Calls;
    public  int                 Passes;

    public override string      ToString() => Append(new StringBuilder()).ToString();
    
    internal StringBuilder Append(StringBuilder sb)
    {
        switch (RecordType) {
            case PipelineRecordType.Kernel:
                sb.Append($"'{KernelName}'  calls: {Calls}  passes: {Passes}");
                break;
            case PipelineRecordType.KernelSubmit:
                sb.Append($"[KernelSubmit]  '{KernelName}'");
                break;
            case PipelineRecordType.BatchSubmit:
                sb.Append($"[BatchSubmit]");
                break;
        }
        return sb;
    } 
}

public class PipelineContext
{
    public    virtual   bool                            EnablePassBatching { get; set; }
    public    virtual   bool                            EnableDiagnostics  { get; set; }
    public              ReadOnlySpan<PipelineRecord>    Records         => GetRecords();
    public              string                          RecordLog       => AppendRecordLog(new StringBuilder()).ToString();
    public    virtual   void                            ClearRecords()  { }
    
    protected virtual   ReadOnlySpan<PipelineRecord>    GetRecords()    => default;

    public    override  string ToString() => $"Batching: {EnablePassBatching}  Diagnostics: {EnableDiagnostics}  Records: {Records.Length}";
    

    private StringBuilder AppendRecordLog(StringBuilder sb)
    {
        sb.Append($"--- PIPELINE TRACE ({this}) ---");
        
        foreach (var record in Records) {
            sb.Append('\n');
            record.Append(sb);
        }
        return sb;
    }
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

