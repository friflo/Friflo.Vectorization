// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Tests.Generators;
using Tests.WGSL;
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
        var projectDir = TestWgslUtils.GetProjectDir();
        var wgslFiles = VerifyShaderUtils.LoadAdditionalFilesRecursive(projectDir);
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
	[Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
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
    [Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void Binding_already_exists(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<VertexData>    triangles,
        [Map(0, 0)] [storage] [Draw]    InBuffer<VertexData>    triangles2,
        [Map(2, 0)] [uniform]           in MyUniforms           myUniform,
        [Map(2, 1)] [uniform]           Vector2                 model_offset);
        
    public struct MyUniforms (Vector4 tint_color)
    {
        public  Vector4 tint_color = tint_color;
    }

    public struct VertexData(Vector4 position, Vector4 color)
    {
        public Vector4 	position    = position;
        public Vector4 	color       = color;
    }
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
    [Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void Binding_not_in_range(RenderPass pass, RenderConfig config,
        [Map(-1,  0)] [storage] [Draw]    InBuffer<VertexData>    triangles,
        [Map(2, 640)] [uniform]           in MyUniforms           myUniform,
        [Map(2,   1)] [uniform]           Vector2                 model_offset);
        
    public struct MyUniforms (Vector4 tint_color)
    {
        public  Vector4 tint_color = tint_color;
    }

    public struct VertexData(Vector4 position, Vector4 color)
    {
        public Vector4 	position    = position;
        public Vector4 	color       = color;
    }
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
	[Shader("~/shaders/parser-crash.vert.wgsl")]
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
        [Map(2, 0)] [uniform]                                                       InBuffer<Vector4>   vertices1,
        [Map(2, 1)] [storage]                                                       InBuffer<Vector4>   vertices2,
        
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
    
    [Test]
    public static async Task  Verify_Shader_Error_parameter_type_errors()
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
    [Shader("~/shaders/tests/testParameterTypes.wgsl")]
    private static partial void ParameterTypeErrors(RenderPass pass, RenderConfig config,
        [Map(2, 0)] [uniform]                                                       object              mvpMatrices,
        [Map(2, 1)] [storage]                                                       object              vertices,
                    [VertexBuffer(-1)]                                              InBuffer<Vector3>   verticesBuffer1,
                    [VertexBuffer(0)]                                               object              verticesBuffer2,
                    [IndexBuffer]                                                   object              indexBuffer1,
                    [IndexBuffer]                                                   InBuffer<float>     indexBuffer2,

        [Map(0, 0)] [texture_1d(ST.f32)]                                            object  texture0,
        [Map(0, 1)] [texture_2d(ST.f32)]                                            object  texture1,
        [Map(0, 2)] [texture_2d_array(ST.i32)]                                      object  texture2,
        [Map(0, 3)] [texture_3d(ST.i32)]                                            object  texture3,
        [Map(0, 4)] [texture_cube(ST.u32)]                                          object  texture4,
        [Map(0, 5)] [texture_cube_array(ST.u32)]                                    object  texture5,
        [Map(0, 6)] [texture_multisampled_2d(ST.i32)]                               object  texture6,
        [Map(0, 7)] [texture_depth_multisampled_2d]                                 object  texture7,
        [Map(0, 8)] [texture_storage_1d(TextureFormat.RGBA32Float, TSA.read)]       object  texture8,
        [Map(0, 9)] [texture_storage_2d(TextureFormat.RGBA8Unorm, TSA.read)]        object  texture9,
        [Map(0,10)] [texture_storage_2d_array(TextureFormat.RGBA8Uint, TSA.write)]  object  texture10,
        [Map(0,11)] [texture_storage_3d(TextureFormat.R32Float, TSA.read_write)]    object  texture11,
        [Map(0,12)] [texture_depth_2d]                                              object  texture12,
        [Map(0,13)] [texture_depth_2d_array]                                        object  texture13,
        [Map(0,14)] [texture_depth_cube]                                            object  texture14,
        [Map(0,15)] [texture_depth_cube_array]                                      object  texture15,
        
        [Map(1, 0)] [sampler]                                                       object  sampler0,
        [Map(1, 1)] [sampler_comparison]                                            object  sampler1);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_invalid_method_parameter()
    {
        await Verify(
"""
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/renderTest/deform.wgsl")]
    [WorkgroupSize(64)]
    private static partial void InvalidMethodParameter(int i);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_invalid_csharp_structs()
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
    [Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<VertexData>    triangles,
        [Map(2, 0)] [uniform]           in NestedStruct         myUniform,
        [Map(2, 1)] [uniform]           EmptyStruct             empty,
        [Map(2, 2)] [uniform]           double                  primitive);
        
    public struct VertexData
    {
        public Vector4 	position;
        public bool 	color;
    }
    
    public struct NestedStruct
    {
        public Child 	child;
    }
    
    public struct Child
    {
        public Vector4 	position;
        public bool 	color;
    }
    
    public struct EmptyStruct;
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_missing_compute_parameter()
    {
        await Verify(
"""
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/renderTest/deform.wgsl")]
    [WorkgroupSize(64, 1, 2)]
    private static partial void MissingComputeParameter();
}
""");
    }
    
    
    [Test]
    public static async Task  Verify_Shader_Error_invalid_WorkgroupSize()
    {
        await Verify(
"""
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/renderTest/deform.wgsl", compute: "cs_main")]
    [WorkgroupSize(64, 1, 2)]
    private static partial void InvalidWorkgroupSize();
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_missing_entry_compute()
    {
        await Verify(
"""
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/renderTest/deform.wgsl", compute: "cs_unknown")]
    [WorkgroupSize(64, 1, 2)]
    private static partial void MissingEntryPoint();
}
""");
    }
        
    [Test]
    public static async Task  Verify_Shader_Error_missing_entry_fragment()
    {
        await Verify(
"""
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/renderTest/deform.wgsl", fragment: "fs_main")]
    private static partial void MissingEntryFragment();
}
""");
    }
        
    [Test]
    public static async Task  Verify_Shader_Error_missing_entry_vertex()
    {
        await Verify(
"""
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/renderTest/deform.wgsl", fragment: "vs_main")]
    private static partial void MissingEntryVertex();
}
""");
    }
        
    [Test]
    public static async Task  Verify_Shader_Error_expect_fragment_attribute()
    {
        await Verify(
"""
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("~/shaders/renderTest/deform.wgsl", fragment: "cs_main")]
    private static partial void ExpectFragmentAttribute();
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_expect_InOutBuffer()
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
    [Shader("~/shaders/renderTest/deform.wgsl", compute: "cs_main")]
    [WorkgroupSize(64, 1, 1)]
    private static partial void ExpectInOutBuffer(PipelineContext computeContext,
        [Map(0, 0)] [storage] [Dispatch]    InBuffer<VertexData>    vertices,
        [Map(0, 1)] [uniform]               TestAddUniform          testUniform,
        [Map(1, 0)] [uniform]               TimeUniform             uniform);
        
    public struct VertexData (Vector4 position, Vector4 color)
    {
        public  Vector4 position = position;
        public  Vector4 color    = color;
    }
    
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct TestAddUniform (float frequency)
    {
        public  float frequency = frequency;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct TimeUniform (float time)
    {
        public  float time = time;
    }
}
""");
    }

    [Test]
    public static async Task  Verify_Shader_Error_storage_TypeMismatch()
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
    [Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void StorageTypeMismatch(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<int>   triangles,
        [Map(2, 0)] [uniform]           in MyUniforms   myUniform,
        [Map(2, 1)] [uniform]           Vector2         model_offset);
        
    public struct MyUniforms (Vector4 tint_color)
    {
        public  Vector4 tint_color = tint_color;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_storage_TypeMismatch_2()
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
    [Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void StorageTypeMismatch_2(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<Vector2>   triangles,
        [Map(2, 0)] [uniform]           in MyUniforms       myUniform,
        [Map(2, 1)] [uniform]           Vector2             model_offset);
        
    public struct MyUniforms (Vector4 tint_color)
    {
        public  Vector4 tint_color = tint_color;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_uniform_TypeMismatch()
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
    [Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void StorageTypeMismatch_2(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [storage] [Draw]    InBuffer<VertexData>   triangles,
        [Map(2, 0)] [uniform]           int                     myUniform,
        [Map(2, 1)] [uniform]           Vector3                 model_offset);
    
    public struct VertexData(Vector4 position, Vector4 color)
    {
        public Vector4 	position    = position;
        public Vector4 	color       = color;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_FixedSizeArrays()
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
    [Shader("~/shaders/tests/testTypeSize.wgsl")]
    public static partial void FixedSizeArrays(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]   in int      uniform0,
        [Map(0, 1)] [uniform]   in float    uniform1);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Error_ConcreteTypes()
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
    [Shader("~/shaders/tests/testTypeSize2.wgsl")]
    public static partial void ConcreteTypes(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]   in Point4       uniform0,
        [Map(0, 1)] [uniform]   in Point3       uniform1,
        [Map(0, 2)] [uniform]   in UniformPoint uniform2);
    
    public struct Point4
    {
        public  int x;
        public  int y;
        public uint z;
        public  int w;
    }
    
    public struct Point3
    {
        public  int x;
        public  int y;
        public  int z;
    }
    
    public struct UniformPoint
    {
        public  Point3 value;
    }
}
""");
    }
}