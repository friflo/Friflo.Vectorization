// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.WGSL;

public sealed partial class WgslVectorizer
{
    public void EmitLoadVector(StringBuilder source, Query query, VectorType vectorType, int step)
    {
        if (!vectorType.IsSpan) {
            source.AppendLine($"        var {vectorType.Name} = uniforms.{vectorType.Name};");
            return;
        }
        source.AppendLine($"        var {vectorType.Name} = {vectorType.Name}_arr[index];");
    }
    
    public void EmitStoreVector(StringBuilder source, Query query, string dirtyVector, int step)
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