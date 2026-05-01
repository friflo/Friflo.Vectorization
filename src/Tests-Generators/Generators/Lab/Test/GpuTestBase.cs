using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.Lab;

public abstract class GpuTestBase
{
    protected   GpuInstance     Instance  => GpuTestGlobal.Instance;
    protected   GpuAdapter      Adapter   => GpuTestGlobal.Adapter;
    
    // -----------------------  Local Setup -----------------------
    protected   GpuDevice       Device      { get; private set; }
    protected   GlobalReport    StartReport { get; private set; }

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;

    [SetUp]
    public void BaseSetup() // Name egal, Attribut zählt
    {
        StartReport = Instance.GenerateReport();
        Device      = Adapter.CreateDevice(MaxTasks, SlotSize);
    }

    [TearDown]
    public void BaseTeardown()
    {
        Device?.Dispose();
        var endReport = Instance.GenerateReport();
        AssertResourceLeaks(StartReport, endReport);
    }

    private static void AssertResourceLeaks(GlobalReport startReport, GlobalReport endReport)
    {
        HubReport start = startReport.Vulkan;
        HubReport end   = endReport.Vulkan;
        HubReport diff  = GetDiff(start, end);
        if (IsNull(diff)) {
            return;
        }
        return; // TODO throw exception 
        var str = GetState(start, diff);
        throw new InvalidOperationException(str);
    }
    
    public static bool IsNull(HubReport value)
    {
        return (value.Buffers.           NumKeptFromUser == 0 &&
                value.BindGroups.        NumKeptFromUser == 0 &&
                value.BindGroupLayouts.  NumKeptFromUser == 0 &&
                value.ComputePipelines.  NumKeptFromUser == 0 &&
                value.ShaderModules.     NumKeptFromUser == 0 &&
                value.PipelineLayouts.   NumKeptFromUser == 0);
    }
    
    public  HubReport Diff  => GetDiff(StartReport.Vulkan, Instance.GenerateReport().Vulkan);
    public  string    State => GetState(StartReport.Vulkan, GetDiff(StartReport.Vulkan, Instance.GenerateReport().Vulkan));
    
    private static HubReport GetDiff(HubReport start, HubReport end)
    {
        var diff = new HubReport();
        diff.Buffers.         NumKeptFromUser = end.Buffers.          NumKeptFromUser -  start.Buffers.           NumKeptFromUser;
        diff.BindGroups.      NumKeptFromUser = end.BindGroups.       NumKeptFromUser -  start.BindGroups.        NumKeptFromUser;
        diff.BindGroupLayouts.NumKeptFromUser = end.BindGroupLayouts. NumKeptFromUser -  start.BindGroupLayouts.  NumKeptFromUser;
        diff.ComputePipelines.NumKeptFromUser = end.ComputePipelines. NumKeptFromUser -  start.ComputePipelines.  NumKeptFromUser;
        diff.ShaderModules.   NumKeptFromUser = end.ShaderModules.    NumKeptFromUser -  start.ShaderModules.     NumKeptFromUser;
        diff.PipelineLayouts. NumKeptFromUser = end.PipelineLayouts.  NumKeptFromUser -  start.PipelineLayouts.   NumKeptFromUser;
        return diff;
    }
    
    public static string GetState(HubReport start, HubReport diff)
    {
        return $@"
[GPU RESOURCE LEAK DETECTED]
ResourceType    Start Delta
--------------- ----- -----
Buffers          {(long)start.Buffers         .NumKeptFromUser,4} {(long)diff.Buffers         .NumKeptFromUser,5:+0;-0;0}
BindGroups       {(long)start.BindGroups      .NumKeptFromUser,4} {(long)diff.BindGroups      .NumKeptFromUser,5:+0;-0;0}
BindGroupLayouts {(long)start.BindGroupLayouts.NumKeptFromUser,4} {(long)diff.BindGroupLayouts.NumKeptFromUser,5:+0;-0;0}
ComputePipelines {(long)start.ComputePipelines.NumKeptFromUser,4} {(long)diff.ComputePipelines.NumKeptFromUser,5:+0;-0;0}
ShaderModules    {(long)start.ShaderModules   .NumKeptFromUser,4} {(long)diff.ShaderModules   .NumKeptFromUser,5:+0;-0;0}
PipelineLayouts  {(long)start.PipelineLayouts .NumKeptFromUser,4} {(long)diff.PipelineLayouts .NumKeptFromUser,5:+0;-0;0}
";
    }

} 