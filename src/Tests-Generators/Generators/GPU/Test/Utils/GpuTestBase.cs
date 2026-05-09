using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.GPU;

public abstract class GpuTestBase
{
    protected   WgpuInstance    Instance  => GpuTestGlobal.Instance;
    protected   WgpuAdapter     Adapter   => GpuTestGlobal.Adapter;
    
    // -----------------------  Local Setup -----------------------
    protected   GpuDevice       Device          { get; private set; }
    protected   GlobalReport    StartReport     { get; private set; }
    public      GpuHandles      Handles         => new (StartReport, Instance.GenerateReport(), GpuTestGlobal.GpuBackendType);

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;

    [SetUp]
    public void BaseSetup() {
        Dbg.Instance    = this;
        StartReport     = Instance.GenerateReport();
        Device          = Adapter.CreateDevice("GpuTestBase", MaxTasks, SlotSize);
    }

    [TearDown]
    public void BaseTeardown()
    {
        Device?.Dispose();
        Device = null;
        
        // Use GC.Collect() and GC.WaitForPendingFinalizers() to force worst case lifecycle scenario of Gpu* classes.
        GC.Collect();
        GC.WaitForPendingFinalizers(); // required to execute ~GpuDevice()
        Dbg.Instance = null;
        
        var finalReport = Instance.GenerateReport();
        var finalDiff   = new GpuHandles(StartReport, finalReport, GpuTestGlobal.GpuBackendType);
        
        AssertResourceLeaks(finalDiff);
    }

    private static void AssertResourceLeaks(GpuHandles handleDiff)
    {
        if (handleDiff.IsDiffNull()) {
            return;
        }
        // return;
        var str = handleDiff.GetState();
        throw new InvalidOperationException(str);
    }
}
