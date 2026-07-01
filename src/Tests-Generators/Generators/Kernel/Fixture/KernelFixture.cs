using System.Linq;
using Friflo.Vectorization.CPU;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using Friflo.Vectorization.WebGPU.Runtime;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToConstant.Global
namespace Kernel;

public enum TestBackend {
    Scalar,
    SIMD,
    WGPU,
    Silk
}

[SetUpFixture]
public sealed class KernelFixture
{
    public static readonly TestBackend TestBackend = TestBackend.WGPU;  // WGPU  Scalar  SIMD   (Silk)
    
    public static   GpuInstance Instance    { get; private set; }
    public static   GpuAdapter  Adapter     { get; private set; }

    [OneTimeSetUp]
    public void RunBeforeAnyTests()
    {
        switch (TestBackend) {
            case TestBackend.Scalar:    SetupCPU(GpuBackendType.Scalar);    break;
            case TestBackend.SIMD:      SetupCPU(GpuBackendType.SIMD);      break;
            case TestBackend.WGPU:      SetupWebGPU();                      break;
            case TestBackend.Silk:      SetupSilk();                        break;
        }
    }
    
    private static void SetupCPU (GpuBackendType backendType) {
        var instance    = CpuInstance.CreateInstance();
        Adapter     = instance.CreateAdapter(backendType);
        Instance    = instance;
    }

    private static readonly GpuBackendType BackendType = GpuBackendType.Undefined;  // D3D12  OpenGL  Vulkan
    
    private static void SetupWebGPU () {
        var instance = WgpuInstance.CreateInstance(new InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        if (BackendType == GpuBackendType.Undefined) {
            Adapter = instance.RequestAdapter(default);
        } else {
            var adapters = instance.GetAdapters();
            Adapter = adapters.FirstOrDefault(props => props.GetAdapterInfo().BackendType == BackendType);
            foreach (var adapter in adapters) { if (Adapter != adapter) adapter.Dispose(); }
        }
        Instance  = instance;
    }
    
    private static void SetupSilk () {
    /*  var instance = SilkWebGPU.SilkInstance.CreateInstance(new Silk.NET.WebGPU.Extensions.WGPU.InstanceExtras {
            // Backends            = InstanceBackend.DX12,
        });
        var infos       = instance.GetAdapterInfos();
        var adapterInfo = infos.FirstOrDefault(props => props.BackendType == GpuBackendType.D3D12);
        Adapter   = instance.RequestAdapter(default, null); // adapterInfo <= use specific adapter
        Instance  = instance; */ 
    }
    
    [OneTimeTearDown]
    public void RunAfterAllTests()
    {
        Adapter.Dispose();
        Instance.Dispose();
    }
}