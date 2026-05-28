// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Text;

// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.Generators;

public static class WgslHelper
{
    public static StringBuilder GenerateWgslHelperMethods(Query query)
    {
        var sb = new StringBuilder();
        if (query.wgslHelperMethods.Count > 0) sb.AppendLine();
        foreach (var helper in query.wgslHelperMethods)
        {
            sb.Append("    ");
            switch (helper) {
                case "distanceSquared2":
                    sb.AppendLine("fn distanceSquared2(a: vec2<f32>, b: vec2<f32>) -> f32 { let d = a - b; return dot(d, d); }");
                    break;
                case "distanceSquared3":
                    sb.AppendLine("fn distanceSquared3(a: vec3<f32>, b: vec3<f32>) -> f32 { let d = a - b; return dot(d, d); }");
                    break;
                case "distanceSquared4":
                    sb.AppendLine("fn distanceSquared4(a: vec4<f32>, b: vec4<f32>) -> f32 { let d = a - b; return dot(d, d); }");
                    break;
                case "cross2d":
                    sb.AppendLine("fn cross2d(a: vec2<f32>, b: vec2<f32>) -> f32 { return a.x * b.y - a.y * b.x; }");
                    break;
                case "log10":
                    sb.AppendLine("fn log10(x: f32) -> f32 { return log(x) / 2.3025851; }");
                    break;
            }
        }
        return sb;
    }
}