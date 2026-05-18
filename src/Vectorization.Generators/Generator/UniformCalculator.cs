// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
    public static int CalculateLayout(List<UniformField> fields)
    {
        return 0;
    }
}
