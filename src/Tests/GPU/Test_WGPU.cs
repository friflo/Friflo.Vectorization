
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
        var config = descSrc.CreateConfig("Custom Config");
        
        Assert.AreEqual("Custom Config", config.Name);
        
        ref readonly var desc = ref config.Descriptor;
        Assert.AreEqual(1, desc.FragmentState!.Value.constants.Length);
        
        
        // --- using a default RenderConfig
        var defaultConfig = new RenderConfig();
        {
            var e = Assert.Throws<NullReferenceException>(() => {
                _ = defaultConfig.Descriptor;
            });
            Assert.AreEqual("when using a default RenderConfig", e!.Message);
        } {
            var e = Assert.Throws<NullReferenceException>(() => {
                _ = defaultConfig.Name;
            });
            Assert.AreEqual("when using a default RenderConfig", e!.Message);
        }
        
        
    }
}