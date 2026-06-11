using System;
using Friflo.Vectorization.GPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Kernel;

public abstract class KernelBase
{
    protected       GpuInstance     Instance  => KernelFixture.Instance;
    protected       GpuAdapter      Adapter   => KernelFixture.Adapter;
    protected       TestBackend     Backend   => KernelFixture.TestBackend;
    
    // -----------------------  Local Setup -----------------------
    protected       GpuDevice       Device          { get; private set; }
    private         GpuHandleDiff   StartHandles    { get; set; }
    public          GpuHandleDiff   HandleDiff      => StartHandles.GetHandleDiff(Adapter.GenerateHandles());
    protected       int             ExpectedCommandBuffers;

    protected virtual int UniformBufferSize => 64 * 1024;
    
    [SetUp]
    public void BaseSetup() {
        Dbg.Instance            = this;
        StartHandles            = Adapter.GenerateHandles();
        ExpectedCommandBuffers  = 0;
        Device                  = Adapter.CreateDevice("GpuTestBase", UniformBufferSize);
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
        
        AssertResourceLeaks(finalDiff, ExpectedCommandBuffers);
    }

    private static void AssertResourceLeaks(GpuHandleDiff handleDiff, int expectedCommandBuffers)
    {
        if (handleDiff.IsDiffZero(expectedCommandBuffers)) {
            return;
        }
        // return;
        var str = handleDiff.GetState("[GPU RESOURCE LEAK DETECTED]");
        throw new InvalidOperationException(str);
    }
}
