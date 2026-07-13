// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Linq;
using System.Text;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;

public static class TypeGenerator
{
    internal static WgslType GetBindingType(WgslModule module, WgslBinding binding)
    {
        switch (binding.AddressSpace)
        {
            case "uniform":
            case "storage":
                var type = module.Structs.FirstOrDefault(s => s.Name == binding.WgslType.Name);
                if (type != null && type.Fields.Count == 1) {
                    var fieldType = type.Fields[0].WgslType;
                    if (fieldType.Name == "array" && fieldType.Generics.Length >= 1) {
                        return fieldType.Generics[0];
                    }
                }
                return binding.WgslType;
        }
        return null;
    }
    
    public static string GenerateCSharpTypes(string wgsl)
    {
        var module = WgslSuperpowerParser.ParseShader(wgsl);
        
        var exportTypes = new HashSet<string>();

        foreach (WgslBinding binding in module.Bindings)
        {
            // Generate on C# types used in bindings
            switch (binding.AddressSpace)
            {
                case "uniform":
                case "storage":
                    var type = GetBindingType(module, binding);
                    if (!TryGetKnownCSharpType(type, out _)) {
                        exportTypes.Add(type.Name);
                    }
                    break;
            }
        }

        var sb = new StringBuilder();
        foreach (var name in exportTypes)
        {
            var type = module.Structs.FirstOrDefault(s => s.Name == name);
            if (type == null) {
                continue;
            }
            sb.Append($"    public struct {type.Name} {{\n");
            foreach (var field in type.Fields) {
                TryGetKnownCSharpType(field.WgslType, out var csType);
                sb.Append($"        public {csType} {field.Name};\n");
            }
            sb.Append("    }\n");
            sb.Append("    \n");
        }
        return sb.ToString();
    }
    
    internal static bool TryGetKnownCSharpType(WgslType wgslType, out string csType)
    {
        var sb = new StringBuilder();
        sb.Append(wgslType.Name);
        var generics = wgslType.Generics;
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
        var typeName = sb.ToString();
        var result = GetCSharpTypeFromWgslType(typeName);
        if (result != null) {
            csType = result;
            return true;
        }
        csType = typeName;
        return false;
    }
    
    private static string GetCSharpTypeFromWgslType(string typeName)
    {
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
        return null;
    }
}