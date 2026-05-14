// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.WGSL;

public static partial class WgslVectorizer
{
    private static void EmitLoadVector(StringBuilder source, Query query, VectorType vectorType)
    {
        if (!vectorType.IsSpan) {
            return;
        }
        source.AppendLine($"        var {vectorType.Name} = {vectorType.Name}_arr[index];");
    }
    
    private static void EmitStoreVector(StringBuilder source, Query query, string dirtyVector)
    {
        var vectorType = query.VectorTypes.FirstOrDefault(v => v.Parameter.Name == dirtyVector);
        if (vectorType == null) {
            return;
        }
        if (!vectorType.IsSpan) {
            return;
        }
        source.AppendLine($"        {dirtyVector}_arr[index] = {dirtyVector};");
    }
}