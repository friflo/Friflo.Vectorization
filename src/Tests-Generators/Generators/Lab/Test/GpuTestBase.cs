using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.Lab;

public abstract class GpuTestBase
{
    protected GpuInstance Instance  => GpuTestGlobal.Instance;
    protected GpuAdapter  Adapter   => GpuTestGlobal.Adapter;
    
    // -----------------------  Local Setup -----------------------
    protected   GpuDevice       Device { get; private set; }
    private     GlobalReport    startReport;

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;

    [SetUp]
    public void BaseSetup() // Name egal, Attribut zählt
    {
        startReport = Instance.GenerateReport();
        Device = Adapter.CreateDevice(MaxTasks, SlotSize);
    }

    [TearDown]
    public void BaseTeardown()
    {
        Device?.Dispose();
        var endReport = Instance.GenerateReport();
        AssertResourceLeaks(startReport, endReport);
    }

    private static void AssertResourceLeaks(GlobalReport startReport, GlobalReport endReport)
    {
        HubReport s = startReport.Vulkan;
        HubReport e = endReport.Vulkan;
        
        var diff = new HubReport();
        diff.Buffers.         NumKeptFromUser = e.Buffers.          NumKeptFromUser -  s.Buffers.           NumKeptFromUser;
        diff.BindGroups.      NumKeptFromUser = e.BindGroups.       NumKeptFromUser -  s.BindGroups.        NumKeptFromUser;
        diff.BindGroupLayouts.NumKeptFromUser = e.BindGroupLayouts. NumKeptFromUser -  s.BindGroupLayouts.  NumKeptFromUser;
        diff.ComputePipelines.NumKeptFromUser = e.ComputePipelines. NumKeptFromUser -  s.ComputePipelines.  NumKeptFromUser;
        diff.ShaderModules.   NumKeptFromUser = e.ShaderModules.    NumKeptFromUser -  s.ShaderModules.     NumKeptFromUser;
        diff.PipelineLayouts. NumKeptFromUser = e.PipelineLayouts.  NumKeptFromUser -  s.PipelineLayouts.   NumKeptFromUser;
        
        if (diff.Buffers.           NumKeptFromUser == 0 &&
            diff.BindGroups.        NumKeptFromUser == 0 &&
            diff.BindGroupLayouts.  NumKeptFromUser == 0 &&
            diff.ComputePipelines.  NumKeptFromUser == 0 &&
            diff.ShaderModules.     NumKeptFromUser == 0 &&
            diff.PipelineLayouts.   NumKeptFromUser == 0)
        {
            return;
        }
        return; // TODO throw exception 
        
        var str = $@"
[GPU RESOURCE LEAK DETECTED]
ResourceType      Start  Delta
------------------------------
Buffers          {(long)s.Buffers         .NumKeptFromUser,4} {(long)diff.Buffers         .NumKeptFromUser,+4}
BindGroups       {(long)s.BindGroups      .NumKeptFromUser,4} {(long)diff.BindGroups      .NumKeptFromUser,+4}
BindGroupLayouts {(long)s.BindGroupLayouts.NumKeptFromUser,4} {(long)diff.BindGroupLayouts.NumKeptFromUser,+4}
ComputePipelines {(long)s.ComputePipelines.NumKeptFromUser,4} {(long)diff.ComputePipelines.NumKeptFromUser,+4}
ShaderModules    {(long)s.ShaderModules   .NumKeptFromUser,4} {(long)diff.ShaderModules   .NumKeptFromUser,+4}
PipelineLayouts  {(long)s.PipelineLayouts .NumKeptFromUser,4} {(long)diff.PipelineLayouts .NumKeptFromUser,+4}
";
        throw new InvalidOperationException(str);
    }

} 