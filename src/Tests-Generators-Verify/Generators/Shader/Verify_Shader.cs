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

public static class Verify_Shader
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
    public static async Task  Verify_Shader_Example()
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
        [Map(2, 0)] [uniform]           in MyUniforms           myUniform,
        [Map(2, 1)] [uniform]           Vector2                 model_offset);
        
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct MyUniforms (Vector4 tint_color)
    {
        [FieldOffset(  0)]  public  Vector4 tint_color = tint_color;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VertexData(Vector4 position, Vector4 color)
    {
        public Vector4 	position    = position;
        public Vector4 	color       = color;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Texture2D()
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
	[Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
	[Shader("~/shaders/texturedCube/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    protected static partial void RenderCube(RenderPass renderPass, RenderConfig renderConfig,
        [Map(0, 0)] [uniform]                   in Uniforms     uniforms,
        [Map(0, 1)] [sampler]                   GpuSampler      smoothFilter,
        [Map(0, 2)] [texture_2d(ST.f32)]        GpuTextureView  material,
                    [VertexBuffer(0)] [Draw]    InBuffer<float> vertices);
    
    public struct Uniforms {
        public Matrix4x4   modelViewProjectionMatrix;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_texture_storage_2d()
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
    [Shader("~/shaders/basic.vert.wgsl",                  vertex:   "main")]
    [Shader("~/shaders/texturedCube/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
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
    const string Uniforms =
"""
[StructLayout(LayoutKind.Explicit, Size = 1024)]
public struct Uniforms (in Matrix4x4_UniArr_16 modelViewProjectionMatrix)
{
    [FieldOffset(  0)]  public  Matrix4x4_UniArr_16 modelViewProjectionMatrix = modelViewProjectionMatrix;
}

[StructLayout(LayoutKind.Explicit, Size = 1024)]
public struct Matrix4x4_UniArr_16
{
    [FieldOffset(0)]  private Matrix4x4 _element0;
}
""";
    [Test]
    public static async Task  Verify_Shader_DrawInstanced()
    {
        await Verify(
$$"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;
{{Uniforms}}

public partial class ShaderExample
{
	[Shader("~/shaders/instancedCube/instanced.vert.wgsl",  vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",      fragment: "main")]
    private static partial void DrawInstanced(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in Uniforms 	uniforms,
                    [VertexBuffer(0)] [Draw]    InBuffer<float> verticesBuffer);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_DrawArgs()
    {
        await Verify(
$$"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;
{{Uniforms}}

public partial class ShaderExample
{
	[Shader("~/shaders/instancedCube/instanced.vert.wgsl",  vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",      fragment: "main")]
    private static partial void DrawCustomDrawArgs(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in Uniforms 	uniforms,
                    [VertexBuffer(0)]   [Draw]  InBuffer<float> verticesBuffer,
                                                DrawArgs        customArgs = default);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_DrawArgsReadOnlySpan()
    {
        await Verify(
$$"""
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;
{{Uniforms}}

public partial class ShaderExample
{
	[Shader("~/shaders/instancedCube/instanced.vert.wgsl",  vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",      fragment: "main")]
    private static partial void DrawCustomDrawArgsReadOnlySpan(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in Uniforms 	        uniforms,
                    [VertexBuffer(0)]   [Draw]  InBuffer<float>         verticesBuffer,
                                                ReadOnlySpan<DrawArgs>  customArgs = default);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_DrawArgsArray()
    {
        await Verify(
$$"""
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;
{{Uniforms}}

public partial class ShaderExample
{
	[Shader("~/shaders/instancedCube/instanced.vert.wgsl",  vertex:   "main")]
	[Shader("~/shaders/vertexPositionColor.frag.wgsl",      fragment: "main")]
    private static partial void DrawCustomDrawArgsArray(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]                   in Uniforms 	uniforms,
                    [VertexBuffer(0)] [Draw]    InBuffer<float> verticesBuffer,
                                                DrawArgs[]      customArgs = default);
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_DrawVertexIndex()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial struct ShaderExample
{
    [Shader("~/shaders/renderTest/raymarcher_no_texture.wgsl")]
    [DrawVertexIndex(3, 1)]
    public static partial void RenderTunnel(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform] in Uniforms    uniforms);
        
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct Uniforms
    {
        public  Vector3     IResolution;
        private float       _pad;       // 16-Byte Alignment for Vector3
        public  float       ITime;
        private Vector3     _pad2;      // fill block for 16 byte alignment
    }
}
""");
    }

    [Test]
    public static async Task  Verify_Shader_ForeignNamespace()
    {
        await Verify(
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;
using ForeignNamespace;
using Other.Namespace;

namespace VerifyShader {
    public partial class ShaderExample
    {
        [Shader("~/shaders/renderTest/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
        public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
            [Map(0, 0)] [storage] [Draw]    InBuffer<VertexData>  triangles,
            [Map(2, 0)] [uniform]           in MyUniform          myUniform,
            [Map(2, 1)] [uniform]           in GlobalUniform      globalUniform);
    }
}

[StructLayout(LayoutKind.Sequential, Size = 8)]
public struct GlobalUniform
{
    public int 	value;
}

namespace Other.Namespace {
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MyUniform
    {
        public Vector4 	tint_color;
    }
}

namespace ForeignNamespace {
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VertexData(Vector4 position, Vector4 color)
    {
        public Vector4 	position    = position;
        public Vector4 	color       = color;
    }
}
""");
    }
    
    

    
    [Test]
    public static async Task  Verify_Shader_NoParameters()
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
    public static partial void DrawTriangles();
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_no_Layouts()
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
    public static partial void NoLayouts(RenderPass pass, RenderConfig config);
}
""");
    }
    
    
    [Test]
    public static async Task  Verify_Shader_IndexBuffer_shadow()
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
    [Shader("~/shaders/shadowMapping/vertexShadow.wgsl",  vertex: "main")]
    private static partial void DrawIndexBufferShadow(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]               in Scene            scene,
        [Map(1, 0)] [uniform]               in Model            model,
                    [VertexBuffer(0)]       InBuffer<Vector3>   verticesBuffer,
                    [IndexBuffer] [Draw]    InBuffer<ushort>    indexBuffer);
    
    [StructLayout(LayoutKind.Sequential, Size = 144)]
    public struct Scene
    {
        public  Matrix4x4 lightViewProjMatrix;
        public  Matrix4x4 cameraViewProjMatrix;
        public  Vector3   lightPos;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct Model {
        public Matrix4x4   modelMatrix;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_IndexBuffer_render()
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
	[Shader("~/shaders/shadowMapping/vertex.wgsl",    vertex:   "main")]
	[Shader("~/shaders/shadowMapping/fragment.wgsl",  fragment: "main")]
    private static partial void Render(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]               in Scene            scene,
        [Map(0, 1)] [texture_depth_2d]      GpuTextureView      textureView,
        [Map(0, 2)] [sampler_comparison]    GpuSampler          sampler,
        [Map(1, 0)] [uniform]               in Model            model,
                    [VertexBuffer(0)]       InBuffer<Vector3>   verticesBuffer,
                    [IndexBuffer] [Draw]    InBuffer<ushort>    indexBuffer);
    
    [StructLayout(LayoutKind.Sequential, Size = 144)]
    public struct Scene {
        public Matrix4x4   lightViewProjMatrix;
        public Matrix4x4   cameraViewProjMatrix;
        public Vector3     lightPos;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct Model {
        public Matrix4x4   modelMatrix;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_DrawIndexedIndirect()
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
    [Shader("~/shaders/tests/vertexShadowIndexedIndirect.wgsl",  vertex: "main")]
    private static partial void DrawIndexedIndirect(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]           in Scene                    scene,
        [Map(1, 0)] [uniform]           in Model                    model,
        [Map(1, 1)] [storage]    [Draw] InBuffer<IndexedIndirect>   indirectBuffer,
                    [VertexBuffer(0)]   InBuffer<Vector3>           verticesBuffer,
                    [IndexBuffer]       InBuffer<ushort>            indexBuffer,
                                        DrawIndirectArgs            args);
    
    [StructLayout(LayoutKind.Sequential, Size = 144)]
    public struct Scene
    {
        public  Matrix4x4 lightViewProjMatrix;
        public  Matrix4x4 cameraViewProjMatrix;
        public  Vector3   lightPos;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct Model {
        public Matrix4x4   modelMatrix;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_DrawIndirect()
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
    [Shader("~/shaders/tests/vertexShadowIndirect.wgsl",  vertex: "main")]
    private static partial void DrawIndirect(RenderPass pass, RenderConfig config,
        [Map(0, 0)] [uniform]           in Scene            scene,
        [Map(1, 0)] [uniform]           in Model            model,
        [Map(1, 1)] [storage]    [Draw] InBuffer<Indirect>  indirectBuffer,
                    [VertexBuffer(0)]   InBuffer<Vector3>   verticesBuffer,
                                        DrawIndirectArgs    args);
    
    [StructLayout(LayoutKind.Sequential, Size = 144)]
    public struct Scene {
        public Matrix4x4   lightViewProjMatrix;
        public Matrix4x4   cameraViewProjMatrix;
        public Vector3     lightPos;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct Model {
        public Matrix4x4   modelMatrix;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_Textures()
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
    private static partial void Textures(RenderPass pass, RenderConfig config,
        [Map(2, 0)] [storage]                                                       InBuffer<Vector4>   vertices1,
        [Map(2, 1)] [uniform]                                                       InBuffer<Vector4>   vertices2,

        [Map(0, 0)] [texture_1d(ST.f32)]                                            GpuTextureView  texture0,
        [Map(0, 1)] [texture_2d(ST.f32)]                                            GpuTextureView  texture1,
        [Map(0, 2)] [texture_2d_array(ST.i32)]                                      GpuTextureView  texture2,
        [Map(0, 3)] [texture_3d(ST.i32)]                                            GpuTextureView  texture3,
        [Map(0, 4)] [texture_cube(ST.u32)]                                          GpuTextureView  texture4,
        [Map(0, 5)] [texture_cube_array(ST.u32)]                                    GpuTextureView  texture5,
        [Map(0, 6)] [texture_multisampled_2d(ST.i32)]                               GpuTextureView  texture6,
        [Map(0, 7)] [texture_depth_multisampled_2d]                                 GpuTextureView  texture7,
        [Map(0, 8)] [texture_storage_1d(TextureFormat.RGBA32Float, TSA.read)]       GpuTextureView  texture8,
        [Map(0, 9)] [texture_storage_2d(TextureFormat.RGBA8Unorm, TSA.read)]        GpuTextureView  texture9,
        [Map(0,10)] [texture_storage_2d_array(TextureFormat.RGBA8Uint, TSA.write)]  GpuTextureView  texture10,
        [Map(0,11)] [texture_storage_3d(TextureFormat.R32Float, TSA.read_write)]    GpuTextureView  texture11,
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
