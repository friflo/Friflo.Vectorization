// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;

// ReSharper disable MergeIntoPattern
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.Generators.AVX;

public sealed partial class AvxVectorizer
{
    public void EmitLoadVector(StringBuilder source, Query query, VectorType vectorType, int step)
    {
        if (!vectorType.IsSpan) {
            return;
        }
        var laneCount   = query.laneCount;
        var name        = vectorType.Name;
        var typeName    = vectorType.Parameter.Type.Name;
        if (vectorType.ParamType == ParamType.Scalar)
        {
            if (query.dirtyVectorsSet.Contains(name)) {
                var loadRequired = query.readVectors.Contains(name);
                if (!loadRequired) {
                    // case: vector is write only
                    var count = vectorType.Dimension == 1 ? query.scalarLaneCount : laneCount;
                    for (int n = 0; n < count; n++) {
                        source.AppendLine($"                    Vector256<float> {name}_{n};  // {typeName}");
                    }
                    source.AppendLine();
                    return;
                }
            }
            switch (query.vectorDimension)
            {
                case 1:
                    for (int n = 0; n < laneCount; n++) {
                        source.AppendLine($"                    Vector256<float> {name}_{n} = Avx.LoadVector256({name}_ptr + {n*step,2});  // {typeName}");
                    }
                    break;
                case 2:
                    if (vectorType.Dimension == 1) {  // SOA
                        source.AppendLine(
$"""
                    Vector256<float> {name}_0 = Avx.LoadVector256({name}_ptr);      // {typeName}
                    Vector256<float> {name}_1 = Avx.LoadVector256({name}_ptr + 8);  // {typeName}
""");
                    } else {
                        source.AppendLine(
$"""
                    Vector256<float> {name}_scalar_01 = Avx.LoadVector256({name}_ptr);      // {typeName}
                    Vector256<float> {name}_scalar_23 = Avx.LoadVector256({name}_ptr + 8);  // {typeName}
                    Vector256<float> {name}_0 = Avx2.PermuteVar8x32({name}_scalar_01, {name}_mask_lo);
                    Vector256<float> {name}_1 = Avx2.PermuteVar8x32({name}_scalar_01, {name}_mask_hi);
                    Vector256<float> {name}_2 = Avx2.PermuteVar8x32({name}_scalar_23, {name}_mask_lo);
                    Vector256<float> {name}_3 = Avx2.PermuteVar8x32({name}_scalar_23, {name}_mask_hi);
""");
                    }
                    break;
                case 3:
                case 4:
                    if (vectorType.Dimension == 1) {  // SOA
                        source.AppendLine(
$"""
                    Vector256<float> {name}_0 = Avx.LoadVector256({name}_ptr);      // {typeName}
""");
                    } else {
                        source.AppendLine($"                    Vector256<float> {name}_scalar = Avx.LoadVector256({name}_ptr);  // {typeName}");
                        for (int n = 0; n < laneCount; n++) {
                            source.AppendLine($"                    Vector256<float> {name}_{n} = Avx2.PermuteVar8x32({name}_scalar, {name}_mask_{n});");
                        }
                    }
                    break;
            }
        } else {
            if (vectorType.Dimension == 2 && vectorType.Layout == VectorLayout.AoSoA) {
                source.AppendLine(
$"""
                    Vector256<float> {name}_0 = Avx.LoadVector256({name}_ptr);      // xxxxxxxx {typeName}
                    Vector256<float> {name}_1 = Avx.LoadVector256({name}_ptr +  8); // yyyyyyyy
                    Vector256<float> {name}_2 = Avx.LoadVector256({name}_ptr + 16); // xxxxxxxx
                    Vector256<float> {name}_3 = Avx.LoadVector256({name}_ptr + 24); // yyyyyyyy
""");
            } else {
                for (int n = 0; n < laneCount; n++) {
                    if (vectorType.Layout == VectorLayout.AoSoA) {
                        source.AppendLine($"                    Vector256<float> {name}_{n} = Avx.LoadVector256({name}_ptr + {n*step,2});   // {typeName}");
                    } else {
                        source.AppendLine($"                    Vector256<float> {name}_{n} = Avx.LoadVector256({name}_ptr + {n*step,2});   // {typeName}");    
                    }
                }
            }
        }
        if (query.useDeinterleave && vectorType.Dimension > 1 ||
            query.strategy == Strategy.MixedAdapter && vectorType.Layout == VectorLayout.AoS)
        {
            switch (query.vectorDimension) {
                case 2:
                    source.AppendLine($"                    ({name}_0, {name}_1) = AvxVector2.Deinterleave({name}_0, {name}_1);");
                    source.AppendLine($"                    ({name}_2, {name}_3) = AvxVector2.Deinterleave({name}_2, {name}_3);");
                    break;
                case 3:
                    source.AppendLine($"                    ({name}_0, {name}_1, {name}_2) = AvxVector3.Deinterleave({name}_0, {name}_1, {name}_2);");
                    break;
                case 4:
                    source.AppendLine($"                    ({name}_0, {name}_1, {name}_2, {name}_3) = AvxVector4.Deinterleave({name}_0, {name}_1, {name}_2, {name}_3);");
                    break;
            }
        }
        source.AppendLine();
    }
    
    public void EmitStoreVector(StringBuilder source, Query query, string dirtyVector, int step)
    {
        var vectorType = query.VectorTypes.FirstOrDefault(v => v.Name == dirtyVector);
        if (vectorType == null) {
            return;
        }
        if (!vectorType.IsSpan) {
            return;
        }
        var name = vectorType.Name;
        if (query.useDeinterleave ||
            query.strategy == Strategy.MixedAdapter && vectorType.Layout == VectorLayout.AoS)
        {
            switch (vectorType.Dimension) {
                case 1:
                    break;
                case 2:
                    source.AppendLine($"                    ({name}_0, {name}_1) = AvxVector2.Interleave({name}_0, {name}_1);");
                    source.AppendLine($"                    ({name}_2, {name}_3) = AvxVector2.Interleave({name}_2, {name}_3);");
                    break;
                case 3:
                    source.AppendLine($"                    ({name}_0, {name}_1, {name}_2) = AvxVector3.Interleave({name}_0, {name}_1, {name}_2);");
                    break;
                case 4:
                    source.AppendLine($"                    ({name}_0, {name}_1, {name}_2, {name}_3) = AvxVector4.Interleave({name}_0, {name}_1, {name}_2, {name}_3);");
                    break;
            }
        }
        if (vectorType.Dimension == 2 && vectorType.Layout == VectorLayout.AoSoA)
        {
                source.AppendLine(
$"""
                    Avx.Store({name}_ptr,      {name}_0); // xxxxxxxx
                    Avx.Store({name}_ptr +  8, {name}_1); // yyyyyyyy
                    Avx.Store({name}_ptr + 16, {name}_2); // xxxxxxxx
                    Avx.Store({name}_ptr + 24, {name}_3); // yyyyyyyy
""");
        } else {
            var laneCount = query.laneCount;
            if (vectorType.Dimension == 1) {
                laneCount = query.vectorDimension switch {
                    2 => 2,
                    3 => 1,
                    4 => 1,
                    _ => laneCount
                };
            }
            for (int n = 0; n < laneCount; n++) {
                if (vectorType.Layout == VectorLayout.AoSoA) {
                    source.AppendLine($"                    Avx.Store({name}_ptr + {n*step,2}, {name}_{n});");
                } else {
                    source.AppendLine($"                    Avx.Store({name}_ptr + {n*step,2}, {name}_{n});");
                }
            }
        }
        source.AppendLine();
    }
}