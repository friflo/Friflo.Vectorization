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

// ReSharper disable InconsistentNaming
namespace Shader;

public static class Verify_Shader
{
    private static async Task Verify([LanguageInjection("csharp")] string code)
    {
        // 1. Setup (Helper method suggested for readability)
        var compilation = VerifyUtils.CreateCompilation(code);
        var generator = new ShaderGen();
        var driver = CSharpGeneratorDriver.Create(generator);

        // 2. Run
        var runResult = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        
        VerifyUtils.CheckOutputCompilation(outputCompilation);

        // 3. Verify (NUnit adapter)
        await Verifier.Verify(runResult).IgnoreGeneratedResult(VerifyUtils.IgnoreStaticSource);
    }
    
    [Test]
    public static async Task  Verify_Shader_Example()
    {
        var code =
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
    [Shader("shaders/triangle.wgsl", vert: "vs_main", frag: "fs_main")]
    public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
        [BindStorage(0, 0)] InBuffer<VertexData>    triangles,
        [BindUniform(1, 0)] MyUniform               myUniform);
        
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct MyUniform
    {
        public Vector4 	tint_color;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct VertexData(Vector4 position, Vector4 color)
    {
        public Vector4 	position    = position;
        public Vector4 	color       = color;
    }
}
""";
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_Shader_Texture2D()
    {
        var code =
"""
using System.Numerics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU;

namespace VerifyShader;

public partial class ShaderExample
{
	[VertexShader  ("shaders/basic.vert.wgsl",                  vert: "main")]
	[FragmentShader("shaders/sampleTextureMixColor.frag.wgsl",  frag: "main")]
    public static partial void RenderCube(RenderPass pass, RenderConfig config,
        [VertexBuffer(0)]           InBuffer<float> vertices,
        [BindUniform     (0, 0)]    Uniforms        uniforms,
        [SamplerFiltering(0, 1)]    GpuSampler      smoothFilter,
        [texture_2d<f32> (0, 2)]    GpuTextureView  material);
        
    [StructLayout(LayoutKind.Sequential)]
    public struct Uniforms {
        public Matrix4x4   modelViewProjectionMatrix;
    }
}
""";
        await Verify(code);
    }

}
