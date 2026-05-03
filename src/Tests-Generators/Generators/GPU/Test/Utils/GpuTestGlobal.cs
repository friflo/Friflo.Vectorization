using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU;

namespace Tests.Generators.GPU;

[SetUpFixture]
public class GpuTestGlobal
{
    public static GpuInstance Instance { get; private set; }
    public static GpuAdapter  Adapter  { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        Instance = GpuInstance.CreateInstance();
        Adapter = Instance.RequestAdapter(new RequestAdapterOptions { 
            PowerPreference = PowerPreference.HighPerformance 
        });
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Adapter.Dispose();
        Instance.Dispose();
    }
}