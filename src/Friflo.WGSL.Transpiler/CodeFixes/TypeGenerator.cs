// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;

public static class TypeGenerator
{
    public static string GenerateCSharpTypes(string wgsl)
    {
        var module = WgslSuperpowerParser.ParseShader(wgsl);
        
        var sb = new StringBuilder();
        sb.Append("    \n");

        foreach (var type in module.Structs)
        {
            sb.Append($"    public struct {type.Name} {{\n");
            foreach (var field in type.Fields) {
                var fieldType = GetFieldType(field);
                sb.Append($"        public {fieldType} {field.Name};\n");
            }
            sb.Append("    }\n");
            sb.Append("    \n");
        }
        return sb.ToString();
    }
    
    private static string GetFieldType(WgslField field)
    {
        var sb = new StringBuilder();
        sb.Append(field.WgslType.Name);
        var generics = field.WgslType.Generics;
        if (generics.Length > 0)
        {
            sb.Append("<");
            foreach (var generic in generics)
            {
                sb.Append(generic.Name);
                sb.Append(',');
            }
            sb.Length -= 1;
            sb.Append(">");
        }

        var typeName =sb.ToString();
        
        switch (typeName)
        {
            case "u32":         return "uint";
            case "i32":         return "int";
            case "f32":         return "float";
            
            case "vec2<f32>":
            case "vec2f":       return "Vector2";

            case "vec3<f32>":
            case "vec3f":       return "Vector3";
            
            case "vec4<f32>":
            case "vec4f":       return "Vector4";
            
            case "mat4x4f":     return "Matrix4x4";
            
        }
        return field.WgslType.Name;
    }
}