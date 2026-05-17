// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Vectorize;

public static class Verify_Vectorize_Errors
{
    private static async Task Verify(string code)
    {
        // 1. Setup (Helper method suggested for readability)
        var compilation = VerifyUtils.CreateCompilation(code);
        var generator = new Gen();
        var driver = CSharpGeneratorDriver.Create(generator);

        // 2. Run
        var runResult = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        
        VerifyUtils.CheckOutputCompilation(outputCompilation);

        // 3. Verify (NUnit adapter)
        await Verifier.Verify(runResult).IgnoreGeneratedResult(VerifyUtils.IgnoreStaticSource);
    }
    
    [Test]
    public static async Task  Verify_MissingSpanParameter()
    {
        var code =
"""
using System.Numerics;
using Friflo.Vectorization;

namespace VerifyVectorize;

public partial class MyExample
{
    [Vectorize]  [OmitHash]
    private static void MissingSpan(ref Vector4 result, Vector4 vec1, Vector4 vec2) {
        result = Vector4.Cross(vec1, vec2);
    }
}
""";
        await Verify(code);
    }
    
    /* [Test]
    public static async Task  Verify_InternalError()
    {
        var code =
"""
using System.Numerics;
using Friflo.Vectorization;

namespace VerifyVectorize;

public partial class MyExample
{
    [Vectorize] [OmitHash]
    private static void InternalError([Span] ref float value) {
        value = CheckInternalError(value);
    }

    public static float CheckInternalError(float value) { return value; }
}
""";
        await Verify(code);
    } */
 
}
