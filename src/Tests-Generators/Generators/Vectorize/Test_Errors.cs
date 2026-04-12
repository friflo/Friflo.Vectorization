// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;
using Friflo.Vectorization;



// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Tests.Generators.Vectorize;


public static partial class Test_Errors
{
    // --- Expect:  ECSGEN006: No Vector method generated - At least one [Span] parameter must be specified
    [Vectorize]  [OmitHash]
    private static void MissingSpan(ref Vector4 result, Vector4 vec1, Vector4 vec2) {
        result = Vector4.Cross(vec1, vec2);
    }

}
