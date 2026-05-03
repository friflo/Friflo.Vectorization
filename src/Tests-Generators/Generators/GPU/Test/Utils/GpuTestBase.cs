using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.GPU;

public abstract class GpuTestBase
{
    protected   GpuInstance     Instance  => GpuTestGlobal.Instance;
    protected   GpuAdapter      Adapter   => GpuTestGlobal.Adapter;
    
    // -----------------------  Local Setup -----------------------
    protected   GpuDevice       Device          { get; private set; }
    protected   GlobalReport    StartReport     { get; private set; }
    public      GpuHandles      Handles         => new (StartReport.Vulkan, Instance.GenerateReport().Vulkan);

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;

    [SetUp]
    public void BaseSetup() {
        Dbg.Instance    = this;
        Device          = Adapter.CreateDevice("GpuTestBase", MaxTasks, SlotSize);
        StartReport     = Instance.GenerateReport();
    }

    [TearDown]
    public void BaseTeardown()
    {
        var finalReport = Instance.GenerateReport();
        var finalDiff   = new GpuHandles(StartReport.Vulkan, finalReport.Vulkan);
        
        Device?.Dispose();
        Device = null;
        
        AssertResourceLeaks(finalDiff);
        
        GC.Collect();
        GC.WaitForPendingFinalizers(); // required to execute ~GpuDevice()
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
