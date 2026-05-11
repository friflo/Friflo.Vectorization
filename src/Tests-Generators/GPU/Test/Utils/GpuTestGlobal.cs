using System.Linq;
using Friflo.Vectorization.GPU;
using NUnit.Framework;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToConstant.Global
namespace Tests.GPU;

public enum TestBackend {
    SIMD,
    WebGPU,
    SilkWebGPU,
}

[SetUpFixture]
public sealed class GpuTestGlobal
{
    public static readonly TestBackend Backend = TestBackend.WebGPU;
    
    public static GpuInstance   Instance    { get; private set; }
    public static GpuAdapter    Adapter     { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        switch (Backend)
        {
            case TestBackend.SIMD: {
                break;
            }
            case TestBackend.SilkWebGPU: {
                var instance = Friflo.Vectorization.SilkWebGPU.WgpuInstance.CreateInstance(new InstanceExtras {
                    // Backends            = InstanceBackend.DX12,
                });
                var infos       = instance.GetAdapterInfos();
                var adapterInfo = infos.FirstOrDefault(props => props.BackendType == Silk.NET.WebGPU.BackendType.D3D12);
                Adapter         = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
                Instance        = instance;
                break;
            }
            case TestBackend.WebGPU:{
                var instance = Friflo.Vectorization.WebGPU.WgpuInstance.CreateInstance(new InstanceExtras {
                    // Backends            = InstanceBackend.DX12,
                });
                var infos       = instance.GetAdapterInfos();
                var adapterInfo = infos.FirstOrDefault(props => props.BackendType == Silk.NET.WebGPU.BackendType.D3D12);
                Adapter         = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
                Instance        = instance;
                break;
            }
        }

    }

    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Adapter.Dispose();
        Instance.Dispose();
    }
}