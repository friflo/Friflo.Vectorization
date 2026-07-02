// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public enum TraceType : byte
{
    Kernel,
    Shader,
    Submit,
    Hazard_RAW,
    Hazard_WAR,
    Hazard_WAW,
    Write,
}

public enum TraceSubType : byte
{
    None,
    NewPass,
    PassSplit,
    Coalescing
}

public struct PipelineStats
{
    /// <summary>Total number of dispatched GPU kernels; higher means more workload processed.</summary>
    public int Calls;

    /// <summary>Total hardware passes generated; target 1 to ensure everything runs in a single batch.</summary>
    public int Passes;

    /// <summary>Total pipeline stalls detected; hunt this down to 0 for maximum performance.</summary>
    public int Hazards;

    public override string ToString() => $"calls: {Calls}  passes: {Passes}  hazards: {Hazards}";
}

public struct PipelineTrace
{
    public  TraceType       TraceType;
    public  string          ShaderName => TraceType == TraceType.Kernel ? KernelRegistry.GetKernelName(ShaderId) : ShaderRegistry.GetShaderName(ShaderId);
    public  int             ShaderId;
    public  int             Calls;
    public  TraceSubType    SubType;
    public  string          Resource;

    public override string  ToString() => Append(new StringBuilder(), 23).ToString();
    
    internal StringBuilder  Append(StringBuilder sb, int indent)
    {
        switch (TraceType) {
            case TraceType.Shader:
            case TraceType.Kernel: {
                var name    = ShaderName;
                var len     = Math.Max(0, indent - name.Length);
                sb.Append($"{name}()").Append(' ', len).Append($" calls: {Calls,2}");
                switch (SubType) {
                    case TraceSubType.NewPass:   sb.Append("   new_pass");   break;
                    case TraceSubType.PassSplit: sb.Append("   pass_split"); break;
                }
                break;
            }
            case TraceType.Submit:
                sb.Append($"> Submit                        commands: {Calls}");
                break;
            case TraceType.Hazard_RAW:
                sb.Append($"  | RAW '{Resource}'");
                break;
            case TraceType.Hazard_WAR:
                sb.Append($"  | WAR '{Resource}'");
                break;
            case TraceType.Hazard_WAW:
                sb.Append($"  | WAW '{Resource}'");
                break;
            case TraceType.Write: {
                var len = Math.Max(0, indent - Resource.Length - 7);
                sb.Append($"> Write '{Resource}'").Append(' ', len).Append($"ranges: {Calls}");
                if (SubType == TraceSubType.Coalescing) {
                    sb.Append(" - coalescing");
                }
                break;
            }
        }
        return sb;
    } 
}

public struct KernelMetric : IComparable<KernelMetric>
{
    public  string  KernelName => KernelRegistry.GetKernelName(KernelId);
    public  int     KernelId;
    public  int     Calls;
    public  int     Passes;
    
    public override  string ToString() => $"{KernelName}()  calls: {Calls}  passes: {Passes}";
    
    public int CompareTo(KernelMetric other) {
        return string.Compare(KernelName, other.KernelName, StringComparison.OrdinalIgnoreCase);
    }
}

public static class KernelRegistry
{
    private static          string[]    kernelNames = new string[20];
    private static readonly object      mutex = new();
    private static          int         nextId;
    
    internal static         string      GetKernelName(int slot) => kernelNames[slot];
    
    [EditorBrowsable(EditorBrowsableState.Never)]
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

public static class ShaderRegistry
{
    private static          string[]    shaderNames = new string[20];
    private static readonly object      mutex = new();
    private static          int         nextId;
    
    internal static         string      GetShaderName(int slot) => shaderNames[slot];
    
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int NewShaderId(string shaderName)
    {
        lock (mutex)
        {
            var newId = ++nextId;
            if (newId >= shaderNames.Length) {
                var newNames = new string[2 * shaderNames.Length];
                Array.Copy(shaderNames, newNames, shaderNames.Length);
                newNames[newId] = shaderName;
                shaderNames = newNames;
            } else {
                shaderNames[newId] = shaderName;
            }
            return newId;
        }
    }
}

