using System.Linq;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Tests.Generators.GPU;

[SetUpFixture]
public sealed class GpuTestGlobal
{
    public static GpuInstance       Instance        { get; private set; }
    public static GpuAdapter        Adapter         { get; private set; }
    public static BackendType       BackendType     { get; private set; }
    public static GpuBackendType    GpuBackendType  { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        Instance = GpuInstance.CreateInstance(new InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        var properties      = Instance.GetAdapterInfos();
        var adapterProperty = properties.FirstOrDefault(props => props.BackendType == BackendType.D3D12);
        Adapter = Instance.RequestAdapter(default, null); // adapterProperty <= use specific adapter
        
        // get type of selected GPU backend
        var props       = Adapter.GetAdapterInfo();
        BackendType     = props.BackendType;
        GpuBackendType	= GpuHandles.GetHandleType(BackendType);
    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Adapter.Dispose();
        Instance.Dispose();
    }
}