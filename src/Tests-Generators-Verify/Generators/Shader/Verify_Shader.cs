// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Friflo;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Tests.Generators;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
namespace Shader;

public static class Verify_Shader
{
    public class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        public  override string         Path { get; } = path;
        public  readonly SourceText     Text = SourceText.From(content);

        public override SourceText GetText(CancellationToken cancellationToken = default) => Text;
    }
    
public static ImmutableArray<AdditionalText> LoadAdditionalFilesRecursive(string srcFolder, string baseFolder)
{
    if (Environment.CurrentDirectory.EndsWith("/linux-x64")) {
        srcFolder = "../" + srcFolder; // use a specific bin folder on GitHub.  See: https://github.com/friflo/Friflo.Vectorization/blob/main/.github/workflows/generators-ci.yml#L55
    }
    var searchPath  = Path.GetFullPath(srcFolder);
    if (!Directory.Exists(searchPath)) {
        throw new InvalidOperationException($"folder not found: searchPath: {searchPath}  CurrentDirectory: {Environment.CurrentDirectory}");
    } 
    var fullBaseDir = searchPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var builder     = ImmutableArray.CreateBuilder<AdditionalText>();

    // iterate recursive all *.wgsl files
    foreach (var fullFilePath in Directory.EnumerateFiles(fullBaseDir, "*.wgsl", SearchOption.AllDirectories))
    {
        var relativePath = baseFolder + Path.GetRelativePath(fullBaseDir, fullFilePath);
        var content = File.ReadAllText(fullFilePath);
        builder.Add(new InMemoryAdditionalText(relativePath, content));
    }
    return builder.ToImmutable();
}
    
    private static async Task Verify([LanguageInjection("csharp")] string code)
    {
        var wgslFiles = LoadAdditionalFilesRecursive("../../../../Tests/shaders", "shaders/");
        
        // 1. Setup (Helper method suggested for readability)
        var compilation = VerifyUtils.CreateCompilation(code);
        var generator = new ShaderGen();
        var driver = CSharpGeneratorDriver.Create(generator).AddAdditionalTexts(wgslFiles);

        // 2. Run
        var runResult = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        
        VerifyUtils.CheckOutputCompilation(outputCompilation);

        // 3. Verify (NUnit adapter)
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
    [Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
        [Draw]  [BindStorage(0, 0)] InBuffer<VertexData>    triangles,
                [BindUniform(1, 0)] in MyUniform            myUniform);
        
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
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    protected static partial void RenderCube(RenderPass pass, RenderConfig config,
        [Draw]  [VertexBuffer(0)]           InBuffer<float> vertices,
                [BindUniform     (0, 0)]    in Uniforms     uniforms,
                [SamplerFiltering(0, 1)]    GpuSampler      smoothFilter,
                [texture_2d(0, 2, ST.f32)]  GpuTextureView  material);
        
    [StructLayout(LayoutKind.Sequential)]
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
	[Shader("~/shaders/sampleTextureMixColor.frag.wgsl",  fragment: "main")]
    protected static partial void RenderCube(RenderPass pass, RenderConfig config,
        [Draw]  [VertexBuffer(0)]                                               InBuffer<float> vertices,
                [BindUniform     (0, 0)]                                        in Uniforms     uniforms,
                [SamplerFiltering(0, 1)]                                        GpuSampler      smoothFilter,
                [texture_storage_2d(0, 2, TextureFormat.RGBA8Unorm, TSA.read)]  GpuTextureView  material);
        
    [StructLayout(LayoutKind.Sequential)]
    public struct Uniforms {
        public Matrix4x4   modelViewProjectionMatrix;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Shader_DrawInstanced()
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
    private static partial void DrawInstanced(RenderPass pass, RenderConfig config,
        [Draw]          [VertexBuffer(0)]   InBuffer<float>     verticesBuffer,
        [DrawInstance]  [BindUniform(0, 0)] InBuffer<Matrix4x4> mvpMatrices);
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
    [Shader("~/shaders/raymarcher_no_texture.wgsl")]
    [DrawVertexIndex(3, 1)]
    public static partial void RenderTunnel(RenderPass pass, RenderConfig config,
        [BindUniform(0, 0)] in Uniforms    uniforms);
        
    [StructLayout(LayoutKind.Sequential)]
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
        [Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
        public static partial void DrawTriangles(RenderPass pass, RenderConfig config,
            [Draw]  [BindStorage(0, 0)] InBuffer<VertexData>    triangles,
                    [BindUniform(1, 0)] in MyUniform            myUniform,
                    [BindUniform(2, 0)] in GlobalUniform        globalUniform);
    }
}

[StructLayout(LayoutKind.Sequential, Size = 4)]
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
        [Draw]  [VertexBuffer(0)]           InBuffer<float> vertices,
                [BindUniform     (0, 0)]    in Uniforms     uniforms,
                [SamplerFiltering(0, 1)]    GpuSampler      smoothFilter,
                [texture_2d(0, 2, ST.f32)]  GpuTextureView  material);
        
    [StructLayout(LayoutKind.Sequential)]
    public struct Uniforms {
        public Matrix4x4   modelViewProjectionMatrix;
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
    [Shader("~/shaders/triangle.wgsl", vertex: "vs_main", fragment: "fs_main")]
    public static partial void DrawTriangles();
}
""");
    }

}
