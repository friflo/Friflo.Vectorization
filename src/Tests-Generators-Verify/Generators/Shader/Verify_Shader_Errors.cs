// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Tests.Generators;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
namespace Shader;

public static class Verify_Shader_Errors
{
    private static async Task Verify([LanguageInjection("csharp")] string code)
    {
        var compilation = VerifyShaderUtils.CreateCompilation(code);
        var generator = new ShaderGen();
        var wgslFiles = VerifyShaderUtils.LoadAdditionalFilesRecursive("../../../../Tests/shaders", "shaders/");
        var driver = CSharpGeneratorDriver.Create(generator).AddAdditionalTexts(wgslFiles);

        // Run
        var runResult = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        
        VerifyUtils.CheckOutputCompilation(outputCompilation);

        // Verify (NUnit adapter)
        await Verifier.Verify(runResult).IgnoreGeneratedResult(VerifyUtils.IgnoreStaticSource);
    }
    
    
    [Test]
    public static async Task  Verify_Shader_Error()
    {
        await Verify(           //   TODO  support having only a [VertexShader]
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[Shader("~/no-file.wgsl",                  vertex: "main")]
    protected static partial void RenderCube(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in Uniforms     uniforms,
        [Map(0, 1)] [sampler]                   GpuSampler      smoothFilter,
        [Map(0, 2)] [texture_2d(ST.f32)]        GpuTextureView  material,
                    [VertexBuffer(0)] [Draw]    InBuffer<float> vertices);
        
    [StructLayout(LayoutKind.Sequential)]
    public struct Uniforms {
        public Matrix4x4   modelViewProjectionMatrix;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_expect_RenderPass()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[Shader("~/tests/noBindings.wgsl", vertex: "vs_main", fragment: "fs_main")]
    protected static partial void Expect_RenderPass(int i);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_expect_two_parameters()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[Shader("~/tests/noBindings.wgsl", vertex: "vs_main", fragment: "fs_main")]
    protected static partial void Expect_RenderPass(RenderPass pass);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_multiple_IndexBuffer_parameters()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    protected static partial void Multiple_IndexBuffer_parameters(RenderPass pass, RenderConfig config,
        [IndexBuffer] [Draw]    InBuffer<ushort>    indexBuffer1,
        [IndexBuffer] [Draw]    InBuffer<ushort>    indexBuffer2);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_binding_already_exists()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[Shader("~/shaders/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    private static partial void Binding_already_exists(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]           [DrawInstance]  InBuffer<Matrix4x4> mvpMatrices,
        [Map(0, 0)] [uniform]           [DrawInstance]  InBuffer<Matrix4x4> mvpMatrices2,
                    [VertexBuffer(0)]   [Draw]          InBuffer<float>     verticesBuffer);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_binding_not_in_range()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[Shader("~/shaders/instanced.vert.wgsl",              vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",    fragment: "main")]
    private static partial void Binding_not_in_range(RenderPass pass, RenderConfig config,
        [Map(-1, 0)][uniform]           [DrawInstance]  InBuffer<Matrix4x4> mvpMatrices,
        [Map(0,640)][uniform]           [DrawInstance]  InBuffer<Matrix4x4> mvpMatrices2,
                    [VertexBuffer(0)]   [Draw]          InBuffer<float>     verticesBuffer);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_WgslParser_Exception()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[Shader("~/shaders/parser-crash.vert.wgsl",              vertex:   "main")]
    private static partial void WgslParser_Exception();
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_TextureTypes()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/tests/testTextureTypes.frag.wgsl",  fragment: "main")]
    public static partial void TestTextureTypes(RenderPass pass, RenderConfig config);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_Texture_type_mismatch()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/tests/testTextureTypes.frag.wgsl")]
    private static partial void TexturesTypeMismatch(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [texture_1d(ST.i32)]                                            GpuTextureView  texture0,
        [Map(0, 1)] [texture_2d(ST.u32)]                                            GpuTextureView  texture1,
        [Map(0, 2)] [texture_2d_array(ST.f32)]                                      GpuTextureView  texture2,
        [Map(0, 3)] [texture_3d(ST.u32)]                                            GpuTextureView  texture3,
        [Map(0, 4)] [texture_cube(ST.i32)]                                          GpuTextureView  texture4,
        [Map(0, 5)] [texture_cube_array(ST.i32)]                                    GpuTextureView  texture5,
        [Map(0, 6)] [texture_multisampled_2d(ST.f32)]                               GpuTextureView  texture6,
        [Map(0, 7)] [texture_depth_multisampled_2d]                                 GpuTextureView  texture7,
        [Map(0, 8)] [texture_storage_1d(TextureFormat.RGBA32Float, TSA.write)]      GpuTextureView  texture8,
        [Map(0, 9)] [texture_storage_2d(TextureFormat.RGBA8UnormSrgb, TSA.read)]    GpuTextureView  texture9,
        [Map(0,10)] [texture_storage_2d_array(TextureFormat.RGBA8Sint, TSA.write)]  GpuTextureView  texture10,
        [Map(0,11)] [texture_storage_3d(TextureFormat.R32Uint, TSA.read_write)]     GpuTextureView  texture11,
        [Map(0,12)] [texture_depth_2d]                                              GpuTextureView  texture12,
        [Map(0,13)] [texture_depth_2d_array]                                        GpuTextureView  texture13,
        [Map(0,14)] [texture_depth_cube]                                            GpuTextureView  texture14,
        [Map(0,15)] [texture_depth_cube_array]                                      GpuTextureView  texture15,
        [Map(1, 0)] [sampler]                                                       GpuSampler      sampler0,
        [Map(1, 1)] [sampler_comparison]                                            GpuSampler      sampler1);
}
""");
    }
}