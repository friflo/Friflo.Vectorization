// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using Friflo.Vectorization;
using NUnit.Framework;


// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Vectorize;



public static partial class Test_Vector3_Avx
{
    // -----------------------------------------------------------------------------------------------------
    [Vectorize] [OmitHash]
    private static void Vector3_Dot([Span] ref float result, [Span] Vector3 vec1, [Span] Vector3 vec2) {
        // result = Vector3.Dot(vec1, vec2);
    }
        
    [Test]
    public static void Test_Vector3_Dot()
    {
        var vec1    = new Vector3[128];
        var vec2    = new Vector3[128];
        var result  = new Vector3[128];
        for (int n = 0; n < 128; n++) {
            vec1[n] = new  Vector3(n, n + 100, n + 200);
            vec2[n] = new  Vector3(n, 2 * n, 3 * n);
        }
        // Vector3_DotVector(vec2,  result, 2);
        
        for (int n = 0; n < 128; n++) {
            // Assert.That(vec1[n], Is.EqualTo(vec2[n]));
        }
    }
}
