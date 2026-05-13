using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

namespace Tests.GPU;

public abstract class GpuTestBase
{
    protected       GpuInstance     Instance  => GpuTestGlobal.Instance;
    protected       GpuAdapter      Adapter   => GpuTestGlobal.Adapter;
    protected       TestBackend     Backend   => GpuTestGlobal.TestBackend;
    
    // -----------------------  Local Setup -----------------------
    protected       GpuDevice       Device          { get; private set; }
    private         GpuHandleDiff   StartHandles    { get; set; }
    public          GpuHandleDiff   HandleDiff      => StartHandles.GetHandleDiff(Adapter.GenerateHandles());

    protected virtual int MaxTasks => 64;
    protected virtual int SlotSize => 64 * 1024;
    
    [SetUp]
    public void BaseSetup() {
        Dbg.Instance    = this;
        StartHandles    = Adapter.GenerateHandles();
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
        var finalDiff   = StartHandles.GetHandleDiff(finalReport);
        
        AssertResourceLeaks(finalDiff);
    }

    private static void AssertResourceLeaks(GpuHandleDiff handleDiff)
    {
        if (handleDiff.IsDiffZero()) {
            return;
        }
        // return;
        var str = handleDiff.GetState("[GPU RESOURCE LEAK DETECTED]");
        throw new InvalidOperationException(str);
    }
}
