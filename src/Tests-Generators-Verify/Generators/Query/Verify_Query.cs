// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Friflo;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using VerifyNUnit;
using VerifyTests;

// ReSharper disable InconsistentNaming
namespace Tests.Generators.Query;

public static class Verify_Query
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
    public static async Task  Verify_Query_MovePosition()
    {
        // 1. Your Input Source
        var code =
"""
using Friflo.Engine.ECS;
using Friflo.Vectorization;

namespace VerifyQuery;

public partial class MyExample
{
    [Query][OmitHash]
    void MoveExample(ref Position position) {
        position.x = 1;
    }
}
""";
        await Verify(code);
    }
    
    [Test]
    public static async Task  Verify_NoComponentParameter()
    {
        var code =
            """
            using Friflo.Engine.ECS;
            using Friflo.Vectorization;
            
            namespace VerifyQuery;

            public partial class MyExample
            {
                [Query][OmitHash]
                private static void NoComponentParameter() { }
            }
            """;
        await Verify(code);
    }
}
