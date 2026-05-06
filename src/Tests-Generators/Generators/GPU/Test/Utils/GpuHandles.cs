using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable RedundantSwitchExpressionArms
namespace Tests.Generators.GPU;

public struct GpuHandle
{
    public  long    Active  { get; private set; }
    public  long    Diff    { get; private set; }

    public override string ToString() => $"{Active}  {Diff,5:+0;-0;0}";
    
    public GpuHandle(RegistryReport start, RegistryReport cur) {
        Active  = (long)start.NumKeptFromUser;
        Diff    = (long)(cur.NumKeptFromUser -  start.NumKeptFromUser);
    }
}


public struct GpuHandles
{
    public  GpuHandle   Devices             { get; private set; }
    public  GpuHandle   Buffers             { get; private set; }
    public  GpuHandle   BindGroups          { get; private set; }
    public  GpuHandle   BindGroupLayouts    { get; private set; }
    public  GpuHandle   ComputePipelines    { get; private set; }
    public  GpuHandle   CommandBuffers      { get; private set; }
    public  GpuHandle   ShaderModules       { get; private set; }
    public  GpuHandle   PipelineLayouts     { get; private set; }
    
    public GpuHandles(in GlobalReport startReport, in GlobalReport curReport, GpuBackendType type)
    {
        var start   = GetReport(startReport, type);
        var cur     = GetReport(curReport, type);
        Devices             = new GpuHandle(start.Devices           , cur.Devices);
        Buffers             = new GpuHandle(start.Buffers           , cur.Buffers);
        BindGroups          = new GpuHandle(start.BindGroups        , cur.BindGroups);
        BindGroupLayouts    = new GpuHandle(start.BindGroupLayouts  , cur.BindGroupLayouts);
        ComputePipelines    = new GpuHandle(start.ComputePipelines  , cur.ComputePipelines);
        CommandBuffers      = new GpuHandle(start.CommandBuffers    , cur.CommandBuffers);
        ShaderModules       = new GpuHandle(start.ShaderModules     , cur.ShaderModules);
        PipelineLayouts     = new GpuHandle(start.PipelineLayouts   , cur.PipelineLayouts);
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
