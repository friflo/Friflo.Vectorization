// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Linq;
using System.Text;

// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable SuggestVarOrType_BuiltInTypes
namespace Friflo.WGSL.Transpiler.CodeFixes;

public readonly struct ShaderTypesResult
{
    public required string  Types       { get; init; }
    public required string  Comments    { get; init; }
}

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
    
    public static ShaderTypesResult GenerateCSharpTypes(string wgsl)
    {
        var module = WgslParser.ParseShader(wgsl);
        
        var exportTypes = new HashSet<string>();

        foreach (WgslBinding binding in module.Bindings)
        {
            // Generate only custom C# types used in bindings
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
        bool addedStructs = false;
        foreach (var name in exportTypes)
        {
            var type = module.Structs.FirstOrDefault(s => s.Name == name);
            if (type == null) {
                continue;
            }
            addedStructs = true;
            var path        = "~/shaders/basic.vert.wgsl";
            sb.Append($"    [Source(\"{path}\")]\n");
            sb.Append($"    [StructLayout(LayoutKind.Sequential)]\n");
            sb.Append($"    public struct {type.Name} {{\n");
            foreach (var field in type.Fields) {
                TryGetKnownCSharpType(field.WgslType, out var csType);
                sb.Append($"        public {csType} {field.Name};\n");
            }
            sb.Append("    }\n");
            sb.Append("    \n");
        }
        
        var comments = "    // [ ]  Remove if you can reuse existing struct types\n";
        if (!addedStructs) {
            comments = "    // (i)  wgsl bindings do not use custom structs\n";
        }
        return new ShaderTypesResult {
            Types       = sb.ToString(),
            Comments    = comments
        };
    }
    
    internal static bool TryGetKnownCSharpType(WgslType wgslType, out string csType)
    {
        var typeName = wgslType.Name;
        var generics = wgslType.Generics;
        if (generics.Length == 1 && generics[0].Name == "f32") {
            typeName = typeName switch
            {
                "vec2"      => "vec2<f32>",
                "vec3"      => "vec3<f32>",
                "vec4"      => "vec4<f32>",
                "mat2x2"    => "mat2x2<f32>",
                "mat3x3"    => "mat3x3<f32>",
                "mat4x4"    => "mat4x4<f32>",
                _           => typeName
            };
        }
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
        return typeName switch {
            "bool"  => "bool",
            "u32"   => "uint",
            "i32"   => "int",
            "f32"   => "float",
            "f16"   => "Half",

            "vec2<f32>" or "vec2f"      => "Vector2",
            "vec3<f32>" or "vec3f"      => "Vector3",
            "vec4<f32>" or "vec4f"      => "Vector4",

        //  "vec2<i32>" or "vec2i"      => "Vector2i",
        //  "vec3<i32>" or "vec3i"      => "Vector3i",
        //  "vec4<i32>" or "vec4i"      => "Vector4i",

            "mat2x2<f32>" or "mat2x2f"  => "Matrix2x2",
            "mat3x3<f32>" or "mat3x3f"  => "Matrix3x3",
            "mat4x4<f32>" or "mat4x4f"  => "Matrix4x4",

            _                           => null
        };
    }
}