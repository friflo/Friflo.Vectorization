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
namespace Kernel;

public static class Verify_Kernel_Float
{
    private static async Task Verify([LanguageInjection("csharp")] string code)
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
        await Verify(
"""
using System.Numerics;
using Friflo.Vectorization;
using Friflo.GPU;

namespace VerifyVectorize;

public partial class MyExample
{
    [Kernel, Vectorize]  [OmitHash]
    void MoveExample([Span] ref float position, [Span] float velocity, float deltaTime) {
        position += velocity * deltaTime;
    }
}
""");

    }
    
    [Test]
    public static async Task  Verify_Kernel_Local_Var()
    {
        await Verify("""
using System.Numerics;
using Friflo.Vectorization;
using Friflo.GPU;

namespace VerifyVectorize;

public partial class MyExample
{
    [Kernel, Vectorize]  [OmitHash]
    void MoveExample([Span] ref float position, float deltaTime) {
        var local = deltaTime;
        position = local;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Kernel_Sign()
    {
        await Verify("""
using System;
using System.Numerics;
using Friflo.Vectorization;
using Friflo.GPU;

namespace VerifyVectorize;

public partial class Kernel_Sign_Example
{
    [Kernel, Vectorize]  [OmitHash]
    void Kernel_Sign([Span] ref float position, float value) {
        var sign = MathF.Sign(value);
        position = sign;
    }
}
""");
    }
    
    [Test]
    public static async Task  Verify_Kernel_KernelOnly()
    {
        await Verify("""
using System;
using System.Numerics;
using Friflo.Vectorization;
using Friflo.GPU;

namespace VerifyVectorize;

public partial class Kernel_Sign_Example
{
    [Kernel]  [OmitHash]
    void KernelOnly([Span] ref float position, float value) {
        var sign = MathF.Sign(value);
        position = sign;
    }
}
""");
    }    

}
