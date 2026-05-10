using System.Linq;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable ConvertToConstant.Global
namespace Tests.GPU;

[SetUpFixture]
public sealed class GpuTestGlobal
{
    public static GpuInstance   Instance    { get; private set; }
    public static GpuAdapter    Adapter     { get; private set; }
    
    public static readonly bool UseSilk = false;

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        if (UseSilk) {
            var instance = Friflo.Vectorization.SilkWebGPU.WgpuInstance.CreateInstance(new InstanceExtras {
                // Backends            = InstanceBackend.DX12,
            });
            var infos       = instance.GetAdapterInfos();
            var adapterInfo = infos.FirstOrDefault(props => props.BackendType == BackendType.D3D12);
            Adapter         = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
            Instance        = instance;
        } else {
            var instance = Friflo.Vectorization.WebGPU.WgpuInstance.CreateInstance(new InstanceExtras {
                // Backends            = InstanceBackend.DX12,
            });
            var infos       = instance.GetAdapterInfos();
            var adapterInfo = infos.FirstOrDefault(props => props.BackendType == BackendType.D3D12);
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