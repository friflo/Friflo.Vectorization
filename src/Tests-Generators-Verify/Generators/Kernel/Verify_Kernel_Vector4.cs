// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Tests.Generators;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable InconsistentNaming
namespace Kernel;

public static class Verify_Kernel_Vector4
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
    
    // [Test]
    public static async Task  Verify_Kernel_AssignScalar()
    {
        var code =
"""
using System.Numerics;
using Friflo.Vectorization;

namespace VerifyVectorize;

public partial class MyExample
{
    [Kernel, Vectorize]  [OmitHash]
    void AssignScalar([Span] ref Vector4 position, [Span] Vector4 velocity) {
        float   dist = Vector4.Distance(position, velocity);
        float   sum  = dist;
    }
}
""";
        await Verify(code);
    }
}
