using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.GPU;

public struct GpuHandle
{
    internal long active;
    internal long diff;

    public override string ToString() => $"{active}  {diff,5:+0;-0;0}";
    
    public GpuHandle(RegistryReport start, RegistryReport cur) {
        active  = (long)start.NumKeptFromUser;
        diff    = (long)(cur.NumKeptFromUser -  start.NumKeptFromUser);
    }
}


public struct GpuHandles
{
    private GpuHandle   buffers;
    private GpuHandle   bindGroups;
    private GpuHandle   bindGroupLayouts;
    private GpuHandle   computePipelines;
    private GpuHandle   shaderModules;
    private GpuHandle   pipelineLayouts;
    
    public GpuHandles(in HubReport start, in HubReport cur)
    {
        buffers             = new GpuHandle(start.Buffers           , cur.Buffers);
        bindGroups          = new GpuHandle(start.BindGroups        , cur.BindGroups);
        bindGroupLayouts    = new GpuHandle(start.BindGroupLayouts  , cur.BindGroupLayouts);
        computePipelines    = new GpuHandle(start.ComputePipelines  , cur.ComputePipelines);
        shaderModules       = new GpuHandle(start.ShaderModules     , cur.ShaderModules);
        pipelineLayouts     = new GpuHandle(start.PipelineLayouts   , cur.PipelineLayouts);
    }
    
    public bool IsDiffNull()
    {
        return (buffers.           diff == 0 &&
                bindGroups.        diff == 0 &&
                bindGroupLayouts.  diff == 0 &&
                computePipelines.  diff == 0 &&
                shaderModules.     diff == 0 &&
                pipelineLayouts.   diff == 0);
    }
    
    public string GetState()
    {
        return $@"
[GPU RESOURCE LEAK DETECTED]
ResourceType    Start Delta
--------------- ----- -----
Buffers          {buffers           .active,4} {buffers           .diff,5:+0;-0;0}
BindGroups       {bindGroups        .active,4} {bindGroups        .diff,5:+0;-0;0}
BindGroupLayouts {bindGroupLayouts  .active,4} {bindGroupLayouts  .diff,5:+0;-0;0}
ComputePipelines {computePipelines  .active,4} {computePipelines  .diff,5:+0;-0;0}
ShaderModules    {shaderModules     .active,4} {shaderModules     .diff,5:+0;-0;0}
PipelineLayouts  {pipelineLayouts   .active,4} {pipelineLayouts   .diff,5:+0;-0;0}
";
    }
}
