using System.Linq;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToConstant.Global
namespace Tests.GPU;

public enum TestBackend {
    SIMD,
    WebGPU,
    Silk
}

[SetUpFixture]
public sealed class GpuTestGlobal
{
    private static  GpuInstance   SimdInstance      { get; set; }
    private static  GpuAdapter    SimdAdapter       { get; set; }
    
    private static  GpuInstance   WebGPUInstance    { get; set; }
    private static  GpuAdapter    WebGPUAdapter     { get; set; }

    private static  GpuInstance   SilkInstance      { get; set; }
    private static  GpuAdapter    SilkAdapter       { get; set; }


    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        SetupSIMD();
        SetupWebGPU();
        // SetupSilk();
    }
    
    private static void SetupSIMD () {
        var instance    = new SimdInstance();
        SimdAdapter     = instance.CreateAdapter();
        SimdInstance    = instance;
    }
    
    private static void SetupWebGPU () {
        var instance = WgpuInstance.CreateInstance(new InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        var infos       = instance.GetAdapterInfos();
        var adapterInfo = infos.FirstOrDefault(props => props.BackendType == GpuBackendType.D3D12);
        WebGPUAdapter   = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
        WebGPUInstance  = instance;
    }
    
    private static void SetupSilk () {
        var instance = Friflo.Vectorization.SilkWebGPU.WgpuInstance.CreateInstance(new Silk.NET.WebGPU.Extensions.WGPU.InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        var infos       = instance.GetAdapterInfos();
        var adapterInfo = infos.FirstOrDefault(props => props.BackendType == GpuBackendType.D3D12);
        SilkAdapter   = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
        SilkInstance  = instance;
    }
    
    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        SilkAdapter?.Dispose();
        SilkInstance?.Dispose();
        
        WebGPUAdapter.Dispose();
        WebGPUInstance.Dispose();
    }

    public static GpuInstance GetInstance(TestBackend backend) {
        return backend switch {
            TestBackend.SIMD    => SimdInstance,
            TestBackend.WebGPU  => WebGPUInstance,
            TestBackend.Silk    => SilkInstance
        };
    }

    public static GpuAdapter GetAdapter(TestBackend backend) {
        return backend switch {
            TestBackend.SIMD    => SimdAdapter,
            TestBackend.WebGPU  => WebGPUAdapter,
            TestBackend.Silk    => SilkAdapter
        };
    }
}