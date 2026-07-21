// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Text;

// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.WGSL;

public static class TypeEmitter
{
    public static string Emit(WgslModule module)
    {
        var sb = new StringBuilder();
        sb.Append("using System.Numerics;\n");
        
        sb.Append("\n");
        
        foreach (var type in module.Structs)
        {
            sb.Append($"public struct {type.Name} {{\n");
            foreach (var field in type.Fields)
            {
                var csharpType = GetCSharpType(field.WgslType);
                sb.Append($"    public {csharpType} {field.Name};\n");
            }
            sb.Append("}\n\n");
        }
        return sb.ToString();
    }
    
    private static string GetCSharpType(WgslType type)
    {
        var generics = type.Generics;
        var arg_0 = generics.Length > 0 ? generics[0].Name : "";
        switch (type.Name)
        {
            case "i32":         return "int";
            case "u32":         return "uint";
            case "f32":         return "float";
            case "f16":         return "Half";
            //
            case "vec2f":       return "Vector2";
            case "vec3f":       return "Vector3";
            case "vec4f":       return "Vector4";
            case "vec2":
                return arg_0 switch {
                    "f32"   => "Vector2",
                    _ => throw new NotImplementedException()
                };
            case "vec3":
                return arg_0 switch {
                    "f32"   => "Vector3",
                    _ => throw new NotImplementedException()
                };
            case "vec4":
                return arg_0 switch {
                    "f32"   => "Vector4",
                    _ => throw new NotImplementedException()
                };
            //
            case "mat4x4f":     return "Matrix4x4";
            default:
                return "int";
        }
    }
}