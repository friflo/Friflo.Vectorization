// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable RedundantSwitchExpressionArms
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public struct GpuHandle
{
    public  long    Active  { get; private set; }
    public  long    Diff    { get; private set; }

    public override string ToString() => $"{Active}  {Diff,5:+0;-0;0}";
    
    public GpuHandle(long active) {
        Active  = active;
    }
    
    public GpuHandle(GpuHandle start, GpuHandle cur) {
        Active  = start.Active;
        Diff    = cur.Active - start.Active;
    }
}


public readonly struct GpuHandleDiff
{
    public  GpuHandle   Devices             { get; init; }
    public  GpuHandle   Buffers             { get; init; }
    public  GpuHandle   BindGroups          { get; init; }
    public  GpuHandle   BindGroupLayouts    { get; init; }
    public  GpuHandle   ComputePipelines    { get; init; }
    public  GpuHandle   CommandBuffers      { get; init; }
    public  GpuHandle   ShaderModules       { get; init; }
    public  GpuHandle   PipelineLayouts     { get; init; }
    
    public GpuHandleDiff GetHandleDiff(in GpuHandleDiff cur)
    {
        return new GpuHandleDiff {
            Devices             = new GpuHandle(Devices,             cur.Devices),
            Buffers             = new GpuHandle(Buffers,             cur.Buffers),
            BindGroups          = new GpuHandle(BindGroups,          cur.BindGroups),
            BindGroupLayouts    = new GpuHandle(BindGroupLayouts,    cur.BindGroupLayouts),
            ComputePipelines    = new GpuHandle(ComputePipelines,    cur.ComputePipelines),
            CommandBuffers      = new GpuHandle(CommandBuffers,      cur.CommandBuffers),
            ShaderModules       = new GpuHandle(ShaderModules,       cur.ShaderModules),
            PipelineLayouts     = new GpuHandle(PipelineLayouts,     cur.PipelineLayouts)
        };
    }
    
    public bool IsDiffNull()
    {
        return (Devices.           Diff == 0 &&
                Buffers.           Diff == 0 &&
                BindGroups.        Diff == 0 &&
                BindGroupLayouts.  Diff == 0 &&
                ComputePipelines.  Diff == 0 &&
                CommandBuffers.    Diff == 0 &&
                ShaderModules.     Diff == 0 &&
                PipelineLayouts.   Diff == 0);
    }
    
    public string GetState()
    {
        return $@"
[GPU RESOURCE LEAK DETECTED]
ResourceType    Start Delta
--------------- ----- -----
Devices          {Devices           .Active,4} {Devices           .Diff,5:+0;-0;0}
Buffers          {Buffers           .Active,4} {Buffers           .Diff,5:+0;-0;0}
BindGroups       {BindGroups        .Active,4} {BindGroups        .Diff,5:+0;-0;0}
BindGroupLayouts {BindGroupLayouts  .Active,4} {BindGroupLayouts  .Diff,5:+0;-0;0}
ComputePipelines {ComputePipelines  .Active,4} {ComputePipelines  .Diff,5:+0;-0;0}
CommandBuffers   {CommandBuffers    .Active,4} {CommandBuffers    .Diff,5:+0;-0;0}
ShaderModules    {ShaderModules     .Active,4} {ShaderModules     .Diff,5:+0;-0;0}
PipelineLayouts  {PipelineLayouts   .Active,4} {PipelineLayouts   .Diff,5:+0;-0;0}
";
    }
}
