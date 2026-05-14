// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Kernel;

public static class Verify_Kernel_Float
{
    private static async Task Verify(string code)
    {
        // 1. Setup (Helper method suggested for readability)
        var compilation = VerifyUtils.CreateCompilation(code);
        var generator = new Gen();
        var driver = CSharpGeneratorDriver.Create(generator);

        // 2. Run
        var runResult = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        
        VerifyUtils.CheckOutputCompilation(outputCompilation);

        // 3. Verify (NUnit adapter)
        await Verifier.Verify(runResult).IgnoreGeneratedResult(VerifyUtils.IgnoreStaticSource);
    }
    
    [Test]
    public static async Task  Verify_Kernel_Multiply()
    {
        var code =
"""
using System.Numerics;
using Friflo.Engine.ECS;
using Friflo.Vectorization;

namespace VerifyVectorize;

public partial class MyExample
{
    [Kernel]  [OmitHash]
    void MoveExample([Span] ref float position, [Span] float velocity, float deltaTime) {
        position += velocity * deltaTime;
    }
}
""";
        await Verify(code);
    }

}
