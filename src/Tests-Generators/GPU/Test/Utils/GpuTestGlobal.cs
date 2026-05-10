using System.Linq;
using Friflo.Vectorization.SilkWebGPU;
using NUnit.Framework;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.GPU;

[SetUpFixture]
public sealed class GpuTestGlobal
{
    public static WgpuInstance  Instance    { get; private set; }
    public static WgpuAdapter   Adapter     { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        Instance = WgpuInstance.CreateInstance(new InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        var infos           = Instance.GetAdapterInfos();
        var adapterProperty = infos.FirstOrDefault(props => props.BackendType == BackendType.D3D12);
        Adapter = Instance.RequestAdapter(default, null); // adapterProperty <= use specific adapter
        
        // get type of selected GPU backend
        var props       = Adapter.GetAdapterInfo();
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Adapter.Dispose();
        Instance.Dispose();
    }
}