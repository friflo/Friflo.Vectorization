using System;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.SilkWebGPU;
using NUnit.Framework;

namespace Tests.GPU;

public abstract class GpuTestBase
{
    protected   WgpuInstance    Instance  => GpuTestGlobal.Instance;
    protected   WgpuAdapter     Adapter   => GpuTestGlobal.Adapter;
    
    // -----------------------  Local Setup -----------------------
    protected   GpuDevice       Device          { get; private set; }
    protected   GpuHandles      StartReport     { get; private set; }
    public      GpuHandles      Handles         => StartReport.GetDiff(Adapter.GenerateHandles());

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;

    [SetUp]
    public void BaseSetup() {
        Dbg.Instance    = this;
        StartReport     = Adapter.GenerateHandles();
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
        
        var finalReport = Adapter.GenerateHandles();
        var finalDiff   = StartReport.GetDiff(finalReport);
        
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
