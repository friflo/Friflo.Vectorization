// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable RedundantSwitchExpressionArms
namespace Tests.GPU;

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


public struct GpuHandles
{
    public  GpuHandle   Devices             { get; set; }
    public  GpuHandle   Buffers             { get; set; }
    public  GpuHandle   BindGroups          { get; set; }
    public  GpuHandle   BindGroupLayouts    { get; set; }
    public  GpuHandle   ComputePipelines    { get; set; }
    public  GpuHandle   CommandBuffers      { get; set; }
    public  GpuHandle   ShaderModules       { get; set; }
    public  GpuHandle   PipelineLayouts     { get; set; }
    

    
    public GpuHandles GetDiff(in GpuHandles cur)
    {
        var result = new GpuHandles();
        result.Devices             = new GpuHandle(Devices,             cur.Devices);
        result.Buffers             = new GpuHandle(Buffers,             cur.Buffers);
        result.BindGroups          = new GpuHandle(BindGroups,          cur.BindGroups);
        result.BindGroupLayouts    = new GpuHandle(BindGroupLayouts,    cur.BindGroupLayouts);
        result.ComputePipelines    = new GpuHandle(ComputePipelines,    cur.ComputePipelines);
        result.CommandBuffers      = new GpuHandle(CommandBuffers,      cur.CommandBuffers);
        result.ShaderModules       = new GpuHandle(ShaderModules,       cur.ShaderModules);
        result.PipelineLayouts     = new GpuHandle(PipelineLayouts,     cur.PipelineLayouts);
        return result;
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
