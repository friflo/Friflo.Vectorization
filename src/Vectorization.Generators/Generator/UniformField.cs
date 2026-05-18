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
    public          int     offset;     // is calculated
    public required int     size;       // 4, 8, 12, 16, ...
    public required int     alignment;  // 4, 8, 16  (note: vec3<f32> size: 12, alignment: 16) 
    public required RefKind refKind;
    public required bool    isCount;
    
    private static (string wgslType, int size, int alignment) WgslTypeFromType(string typeName)
    {
        return typeName switch
        {
            // scalar
            "float" or "global::System.Single"      => ("f32",        4,  4),
            "int"   or "global::System.Int32"       => ("i32",        4,  4),
            "uint"  or "global::System.UInt32"      => ("u32",        4,  4),
            "bool"  or "global::System.Boolean"     => ("bool",       4,  4), // In WGSL bool in Uniforms 4 Bytes
            // Vector2
            "global::System.Numerics.Vector2"       => ("vec2<f32>",  8,  8),
            // Vector3 (special case in WGSL: size 12, but alignment 16!)
            "global::System.Numerics.Vector3"       => ("vec3<f32>", 12, 16),
            // Vector4
            "global::System.Numerics.Vector4"       => ("vec4<f32>", 16, 16),
            
            _ => throw new NotSupportedException($"Type '{typeName}' not supported.")
        };
    }
    
    public static int GetUniformFields(Query query, List<UniformField> fields)
    {
        fields.Add(new UniformField {
            name        = "count",
            type        = "int",
            wgslType    = "u32",
            size        = 4,
            alignment   = 4, 
            refKind     = RefKind.None,
            isCount     = true
        });

        foreach (var vectorType in query.VectorTypes) {
            if (vectorType.IsSpan) {
                continue;
            }
            var parameter = vectorType.Parameter;
            var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            
            (string wgslType, int size, int alignment) = WgslTypeFromType(typeName);
            
            var field = new UniformField {
                name        = parameter.Name,
                type        = typeName,
                wgslType    = wgslType, 
                size        = size,
                alignment   = alignment, 
                refKind     = parameter.RefKind,
                isCount     = false
            };
            fields.Add(field);
        }
        return CalculateLayout(fields);
    }

    // "Greedy-Packing-Strategy"
    // Creates optimal struct layout with
    // - 16-byte alignment (std140/std430)
    // - correct field alignment
    private static int CalculateLayout(List<UniformField> fields)
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
        
        fields.Clear();
        fields.AddRange(orderedFields); // update original list
        
        // Write back the ordered results to the original list references
        return currentOffset;
    }
}
