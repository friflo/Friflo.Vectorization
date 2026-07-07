
using System;
using Friflo.Vectorization.WebGPU;
using NUnit.Framework;

// ReSharper disable UseObjectOrCollectionInitializer
// ReSharper disable InconsistentNaming
namespace Tests.GPU;

public static class Test_WGPU
{
    [Test]
    public static void Test_WGPU_RenderConfig()
    {
        var descSrc = new GpuRenderPipelineDescriptor {
            FragmentState = new GpuFragmentState {
                constants = [new GpuConstantEntry { key = "test", value = 123 }] // add test entry    
            }
        };
        Assert.IsFalse(descSrc.MultisampleState.alphaToCoverageEnabled);
        
        var config = descSrc.CreateConfig("config");
        Assert.AreEqual("config", config.Name);
        
        descSrc.MultisampleState.alphaToCoverageEnabled = true;
        Assert.IsFalse(config.Descriptor.MultisampleState.alphaToCoverageEnabled); // config.Descriptor stays unchanged
        
        ref readonly var desc = ref config.Descriptor;
        Assert.AreEqual(1, desc.FragmentState!.Value.constants.Length);
        
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
    
    struct TestStruct
    {
        public ValueNullable<int>   integer = null;

        public TestStruct() { }
    }
    
    [Test]
    public static void Test_WGPU_ValueNullable()
    {
        var test = new TestStruct();
        var e = Assert.Throws<InvalidOperationException>(() => {
            _ = test.integer.Value;
        });
        Assert.AreEqual("Nullable object must have a value.", e!.Message);
        
        test.integer = 1;
        Assert.AreEqual(1, test.integer.Value);
    }
}