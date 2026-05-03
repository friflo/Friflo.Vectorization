using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable RedundantSwitchExpressionArms
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
    private GpuHandle   devices;
    private GpuHandle   buffers;
    private GpuHandle   bindGroups;
    private GpuHandle   bindGroupLayouts;
    private GpuHandle   computePipelines;
    private GpuHandle   commandBuffers;
    private GpuHandle   shaderModules;
    private GpuHandle   pipelineLayouts;
    
    public GpuHandles(in GlobalReport startReport, in GlobalReport curReport, GpuBackendType type)
    {
        var start   = GetReport(startReport, type);
        var cur     = GetReport(curReport, type);
        devices             = new GpuHandle(start.Devices           , cur.Devices);
        buffers             = new GpuHandle(start.Buffers           , cur.Buffers);
        bindGroups          = new GpuHandle(start.BindGroups        , cur.BindGroups);
        bindGroupLayouts    = new GpuHandle(start.BindGroupLayouts  , cur.BindGroupLayouts);
        computePipelines    = new GpuHandle(start.ComputePipelines  , cur.ComputePipelines);
        commandBuffers      = new GpuHandle(start.CommandBuffers    , cur.CommandBuffers);
        shaderModules       = new GpuHandle(start.ShaderModules     , cur.ShaderModules);
        pipelineLayouts     = new GpuHandle(start.PipelineLayouts   , cur.PipelineLayouts);
    }
    
    public bool IsDiffNull()
    {
        return (devices.           diff == 0 &&
                buffers.           diff == 0 &&
                bindGroups.        diff == 0 &&
                bindGroupLayouts.  diff == 0 &&
                computePipelines.  diff == 0 &&
                commandBuffers.    diff == 0 &&
                shaderModules.     diff == 0 &&
                pipelineLayouts.   diff == 0);
    }
    
    public string GetState()
    {
        return $@"
[GPU RESOURCE LEAK DETECTED]
ResourceType    Start Delta
--------------- ----- -----
Devices          {devices           .active,4} {devices           .diff,5:+0;-0;0}
Buffers          {buffers           .active,4} {buffers           .diff,5:+0;-0;0}
BindGroups       {bindGroups        .active,4} {bindGroups        .diff,5:+0;-0;0}
BindGroupLayouts {bindGroupLayouts  .active,4} {bindGroupLayouts  .diff,5:+0;-0;0}
ComputePipelines {computePipelines  .active,4} {computePipelines  .diff,5:+0;-0;0}
CommandBuffers   {commandBuffers    .active,4} {commandBuffers    .diff,5:+0;-0;0}
ShaderModules    {shaderModules     .active,4} {shaderModules     .diff,5:+0;-0;0}
PipelineLayouts  {pipelineLayouts   .active,4} {pipelineLayouts   .diff,5:+0;-0;0}
";
    }
    
    private static HubReport GetReport(GlobalReport report, GpuBackendType type)
    {
        return type switch {
            GpuBackendType.Vulkan   => report.Vulkan,
            GpuBackendType.Metal    => report.Metal,
            GpuBackendType.Dx12     => report.Dx12,
            GpuBackendType.Gl       => report.Gl,
            _                       => report.Gl,
        };
    }
    
    public static GpuBackendType GetHandleType(BackendType backendType)
    {
        return backendType switch {
            BackendType.D3D11       => GpuBackendType.Dx12,
            BackendType.D3D12       => GpuBackendType.Dx12,
            BackendType.Force32     => GpuBackendType.Gl,
            BackendType.Metal       => GpuBackendType.Metal,
            BackendType.Null        => GpuBackendType.Gl,
            BackendType.OpenGL      => GpuBackendType.Gl,
            BackendType.OpenGles    => GpuBackendType.Gl,
            BackendType.Undefined   => GpuBackendType.Gl,
            BackendType.Vulkan      => GpuBackendType.Vulkan,
            BackendType.WebGpu      => GpuBackendType.Gl,
            _                       => GpuBackendType.Gl
        };
    }
}

public enum GpuBackendType
{
    Vulkan,
    Metal,
    Dx12,
    Gl
}
