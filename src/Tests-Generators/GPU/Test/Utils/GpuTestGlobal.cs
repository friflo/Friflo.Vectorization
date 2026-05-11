using System.Linq;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.GPU.Runtime;
using NUnit.Framework;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToConstant.Global
namespace Tests.GPU;

public enum TestBackend {
    SIMD,
    WebGPU,
    Silk,
}

[SetUpFixture]
public sealed class GpuTestGlobal
{
    public static readonly TestBackend TestBackend = TestBackend.WebGPU;
    
    public static GpuInstance   Instance    { get; private set; }
    public static GpuAdapter    Adapter     { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        switch (TestBackend) {
            case TestBackend.SIMD:      SetupSIMD();    break;
            case TestBackend.WebGPU:    SetupWebGPU();  break;
            case TestBackend.Silk:      SetupSilk();    break;
        }
    }
    
    private static void SetupSIMD () {
        var instance = new SimdInstance();
        Adapter = instance.CreateAdapter();
        Instance = instance;
    }
    
    private static void SetupWebGPU () {
        var instance = Friflo.Vectorization.WebGPU.WgpuInstance.CreateInstance(new InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        var infos       = instance.GetAdapterInfos();
        var adapterInfo = infos.FirstOrDefault(props => props.BackendType == BackendType.D3D12);
        Adapter         = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
        Instance        = instance;
    }
    
    private static void SetupSilk () {
        var instance = Friflo.Vectorization.SilkWebGPU.WgpuInstance.CreateInstance(new InstanceExtras {
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