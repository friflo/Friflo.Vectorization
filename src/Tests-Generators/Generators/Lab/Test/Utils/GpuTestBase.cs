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
    protected   GpuDevice       Device          { get; private set; }
    protected   GlobalReport    StartReport     { get; private set; }
    protected   GpuHandles      StartHandles    => new (StartReport.Vulkan, Instance.GenerateReport().Vulkan);

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;

    [SetUp]
    public void BaseSetup() {
        StartReport     = Instance.GenerateReport();
        Device          = Adapter.CreateDevice(MaxTasks, SlotSize);
    }

    [TearDown]
    public void BaseTeardown()
    {
        Device?.Dispose();
        var endReport = Instance.GenerateReport();
        AssertResourceLeaks(StartHandles, endReport);
    }

    private static void AssertResourceLeaks(GpuHandles start, GlobalReport end)
    {
        start.CalcDiff(end.Vulkan);
        if (start.IsDiffNull()) {
            return;
        }
        return; // TODO throw exception 
        var str = start.GetState();
        throw new InvalidOperationException(str);
    }
}
