// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Linq;
using System.Text;

// ReSharper disable ForCanBeConvertedToForeach
namespace Friflo.Vectorization.Generators;

public static partial class Vectorizer
{
    private static void EmitLoadVector(StringBuilder source, Query query, VectorType vectorType, int step)
    {
        if (!vectorType.isSpan) {
            return;
        }
        var laneCount = query.laneCount;
        var name = vectorType.parameter.Name;
        var typeName = vectorType.parameter.Type.Name;
        if (vectorType.paramType == ParamType.Scalar)
        {
            if (query.dirtyVectorsSet.TryGetValue(vectorType.parameter.Name, out var loadRequired)) {
                if (!loadRequired) {
                    // case: vector is write only
                    var count = vectorType.dimension == 1 ? query.scalarLaneCount : laneCount;
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
                    if (vectorType.dimension == 1) {  // SOA
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
                    if (vectorType.dimension == 1) {  // SOA
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
            if (vectorType.dimension == 2 && vectorType.layout == VectorLayout.SoA) {
                source.AppendLine(
$"""
                    Vector256<float> {name}_0 = Avx.LoadVector256({name}_ptr);      // xxxxxxxx {typeName}
                    Vector256<float> {name}_2 = Avx.LoadVector256({name}_ptr + 8);  // xxxxxxxx
                    Vector256<float> {name}_1 = Avx.LoadVector256({name}_ptr + {name}_stride    ); // yyyyyyyy
                    Vector256<float> {name}_3 = Avx.LoadVector256({name}_ptr + {name}_stride + 8); // yyyyyyyy
""");
            } else {
                for (int n = 0; n < laneCount; n++) {
                    if (vectorType.layout == VectorLayout.SoA) {
                        source.AppendLine($"                    Vector256<float> {name}_{n} = Avx.LoadVector256({name}_ptr + {name}_stride * {n});   // {typeName}");
                    } else {
                        source.AppendLine($"                    Vector256<float> {name}_{n} = Avx.LoadVector256({name}_ptr + {n*step,2});   // {typeName}");    
                    }
                }
            }
        }
        if (query.useDeinterleave && vectorType.dimension > 1 ||
            query.strategy == Strategy.MixedAdapter && vectorType.layout == VectorLayout.AoS)
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
    
    private static void EmitStoreVector(StringBuilder source, Query query, string dirtyVector, int step)
    {
        var vectorType = query.vectorTypes.FirstOrDefault(v => v.parameter.Name == dirtyVector);
        if (vectorType == null) {
            return;
        }
        if (!vectorType.isSpan) {
            return;
        }
        var name = vectorType.parameter.Name;
        if (query.useDeinterleave ||
            query.strategy == Strategy.MixedAdapter && vectorType.layout == VectorLayout.AoS)
        {
            switch (vectorType.dimension) {
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
        if (vectorType.dimension == 2 && vectorType.layout == VectorLayout.SoA)
        {
                source.AppendLine(
$"""
                    Avx.Store({name}_ptr,     {name}_0); // xxxxxxxx
                    Avx.Store({name}_ptr + 8, {name}_2); // xxxxxxxx
                    Avx.Store({name}_ptr + {name}_stride    , {name}_1); // yyyyyyyy
                    Avx.Store({name}_ptr + {name}_stride + 8, {name}_3); // yyyyyyyy
""");
        } else {
            var laneCount = query.laneCount;
            if (vectorType.dimension == 1) {
                laneCount = query.vectorDimension switch {
                    2 => 2,
                    3 => 1,
                    4 => 1,
                    _ => laneCount
                };
            }
            for (int n = 0; n < laneCount; n++) {
                if (vectorType.layout == VectorLayout.SoA) {
                    source.AppendLine($"                    Avx.Store({name}_ptr + {name}_stride * {n}, {name}_{n});");
                } else {
                    source.AppendLine($"                    Avx.Store({name}_ptr + {n*step,2}, {name}_{n});");
                }
            }
        }
        source.AppendLine();
    }
}