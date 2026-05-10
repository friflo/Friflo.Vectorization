using System.Linq;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.SilkWebGPU;
using NUnit.Framework;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.GPU;

[SetUpFixture]
public sealed class GpuTestGlobal
{
    public static GpuInstance   Instance    { get; private set; }
    public static GpuAdapter    Adapter     { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        var instance = WgpuInstance.CreateInstance(new InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        var infos       = instance.GetAdapterInfos();
        var adapterInfo = infos.FirstOrDefault(props => props.BackendType == BackendType.D3D12);
        Adapter         = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
        Instance        = instance;
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Adapter.Dispose();
        Instance.Dispose();
    }
}