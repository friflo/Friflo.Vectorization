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
    public      GpuHandles      HandleDiff      => new (StartReport.Vulkan, Instance.GenerateReport().Vulkan);

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;

    [SetUp]
    public void BaseSetup() {
        StartReport     = Instance.GenerateReport();
        Dbg.Instance    = this;
        Device          = Adapter.CreateDevice(MaxTasks, SlotSize);
    }

    [TearDown]
    public void BaseTeardown()
    {
        Device?.Dispose();
        AssertResourceLeaks(HandleDiff);
        Dbg.Instance = null;
    }

    private static void AssertResourceLeaks(GpuHandles handleDiff)
    {
        if (handleDiff.IsDiffNull()) {
            return;
        }
        return; // TODO throw exception 
        var str = handleDiff.GetState();
        throw new InvalidOperationException(str);
    }
}
