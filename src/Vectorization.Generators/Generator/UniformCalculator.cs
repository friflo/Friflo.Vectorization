// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.Generators;


public class UniformField
{
    public required string  name;
    public required string  type;       // C# type
    public required string  wgslType;   // i32, f32, vec2<f32>, vec3<f32>, ...
    public          int     offset;
    public required int     size;       // 4, 8, 12, 16, ...
    public required int     alignment;  // 4, 8, 16  (note: vec3<f32> size: 12, alignment: 16) 
    public required RefKind refKind;
    public required bool    isCount;
}

public static class UniformCalculator
{
    // "Greedy-Packing-Strategy"
    // Creates optimal struct layout with
    // - 16-byte alignment (std140/std430)
    // - correct field alignment
    public static int CalculateLayout(List<UniformField> fields)
    {
        if (fields.Count == 0) return 0;

        // 1. Sort by alignment descending to minimize padding gaps naturally
        // If alignments are equal, sort by size descending
        var orderedFields = fields
            .OrderByDescending(f => f.alignment)
            .ThenByDescending(f => f.size)
            .ToList();

        int currentOffset = 0;
        int maxAlignment = 16; // WGSL uniform buffers require minimum 16-byte base alignment

        foreach (var field in orderedFields) {
            maxAlignment = Math.Max(maxAlignment, field.alignment);

            // 2. Align current offset to the field's required alignment
            int remainder = currentOffset % field.alignment;
            if (remainder != 0) {
                currentOffset += (field.alignment - remainder);
            }
            // 3. Assign calculated offset
            field.offset = currentOffset;
            // 4. Advance offset by the actual size of the data
            currentOffset += field.size;
        }
        // 5. Pad the final structure size to a multiple of the largest alignment
        int finalRemainder = currentOffset % maxAlignment;
        if (finalRemainder != 0) {
            currentOffset += (maxAlignment - finalRemainder);
        }
        // Write back the ordered results to the original list references
        return currentOffset;
    }
}
