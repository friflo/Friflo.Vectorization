
using System;
using Friflo.Vectorization.WebGPU;
using NUnit.Framework;

// ReSharper disable InconsistentNaming
namespace Tests.GPU;

public static class Test_WGPU
{
    [Test]
    public static void Test_WGPU_RenderConfig()
    {
        var descSrc = new WgpuRenderPipelineDescriptor {
            FragmentState = new WgpuFragmentState {
                constants = [new WgpuConstantEntry { key = "test", value = 123 }] // add test entry    
            }
        };
        var config = descSrc.CreateConfig("config");
        
        Assert.AreEqual("config", config.Name);
        
        ref readonly var desc = ref config.Descriptor;
        Assert.AreEqual(1, desc.FragmentState!.Value.constants.Length);
        
        descSrc.MultisampleState.alphaToCoverageEnabled = true;
        var mutatedConfig = descSrc.CreateConfig("mutatedConfig");
        Assert.AreEqual("mutatedConfig", mutatedConfig.Name);
        
        // --- using a default RenderConfig
        var defaultConfig = new RenderConfig();
        {
            var e = Assert.Throws<NullReferenceException>(() => {
                _ = defaultConfig.Descriptor;
            });
            Assert.AreEqual("using a default RenderConfig", e!.Message);
        } {
            var e = Assert.Throws<NullReferenceException>(() => {
                _ = defaultConfig.Name;
            });
            Assert.AreEqual("using a default RenderConfig", e!.Message);
        }
    }
}