using System.Linq;
using Friflo.Vectorization.GPU;
using NUnit.Framework;


namespace Tests.GPU;

[SetUpFixture]
public sealed class GpuTestGlobal
{
    public static GpuInstance   Instance    { get; private set; }
    public static GpuAdapter    Adapter     { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        if (true) {
            var instance = Friflo.Vectorization.SilkWebGPU.WgpuInstance.CreateInstance(new Silk.NET.WebGPU.Extensions.WGPU.InstanceExtras {
                // Backends            = InstanceBackend.DX12,
            });
            var infos       = instance.GetAdapterInfos();
            var adapterInfo = infos.FirstOrDefault(props => props.BackendType == Silk.NET.WebGPU.BackendType.D3D12);
            Adapter         = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
            Instance        = instance;
        } else {
            var instance = Friflo.Vectorization.WebGPU.WgpuInstance.CreateInstance(new Silk.NET.WebGPU.Extensions.WGPU.InstanceExtras {
                // Backends            = InstanceBackend.DX12,
            });
            var infos       = instance.GetAdapterInfos();
            var adapterInfo = infos.FirstOrDefault(props => props.BackendType == Silk.NET.WebGPU.BackendType.D3D12);
            Adapter         = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
            Instance        = instance;
        }
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Adapter.Dispose();
        Instance.Dispose();
    }
}