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

public static class Verify_Shader_Compute
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
    public static async Task  Verify_Shader_Compute_Deform()
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
    private static partial void DeformVertices(PipelineContext computeContext,
        [Map(0, 0)] [storage] [Dispatch]    InOutBuffer<VertexData> vertices,
        [Map(0, 1)] [uniform]               TestAddUniform          testUniform,
        [Map(1, 0)] [uniform]               TimeUniform             uniform);
        
    public struct VertexData (Vector4 position, Vector4 color)
    {
        public  Vector4 position = position;
        public  Vector4 color    = color;
    }
    
    public struct TestAddUniform (float frequency)
    {
        public  float frequency = frequency;
    }

    public struct TimeUniform (float time)
    {
        public  float time = time;
    }
}
""");
    }
}
