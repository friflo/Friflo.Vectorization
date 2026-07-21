// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ReSharper disable InlineTemporaryVariable
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.WGSL;

public readonly struct StructSource
{
    public required string StructName   { get; init; }
    public required string Source       { get; init; }
} 

public sealed class TypeEmitter
{
    private readonly    WgslModule                  module;
    private readonly    Dictionary<string, string>  structMap   = new();
    
    public TypeEmitter (WgslModule module) {
        this.module = module;
    }
        
    public StructSource[] Emit()
    {
        var structs = module.Structs;
        var bindings = module.Bindings;
        if (bindings.Count == 0 || structs.Count == 0) {
            return null;
        }
        foreach (var binding in bindings)
        {
            var typeName = binding.WgslType.Name;
            var wgslStruct = structs.FirstOrDefault(s => s.Name == typeName);
            if (wgslStruct == null) continue;
            AddStruct(wgslStruct);
        }
        if (structMap.Count == 0) {
            return null;
        }
        return structMap.Select((kv) => new StructSource { StructName = kv.Key, Source = kv.Value}).ToArray();
    }
    
    private void AddStruct(WgslStruct wgslStruct)
    {
        if (structMap.ContainsKey(wgslStruct.Name)) {
            return;
        }
        var sb = new StringBuilder();
        sb.Clear();
        sb.Append($"public struct {wgslStruct.Name} {{\n");
        foreach (var field in wgslStruct.Fields)
        {
            var csharpType = GetCSharpType(field.WgslType);
            sb.Append($"    public {csharpType} {field.Name};\n");
        }
        sb.Append("}\n\n");
        structMap.Add(wgslStruct.Name, sb.ToString());
    }
    
    private string GetCSharpType(WgslType type)
    {
        var generics = type.Generics;
        var arg_0 = generics.Length > 0 ? generics[0].Name : "";
        
        return GetType(type.Name, arg_0);
    }
    
    private string GetType(string typeName, string arg_0)
    {
        switch (typeName)
        {
            case "i32":         return "int";
            case "u32":         return "uint";
            case "f32":         return "float";
            case "f16":         return "Half";
            //
            case "vec2f":       return "Vector2";
            case "vec3f":       return "Vector3";
            case "vec4f":       return "Vector4";
            //
            case "mat4x4f":     return "Matrix4x4";
            //
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
            case "array":
                return GetType(arg_0, null);
            default:
                var wgslStruct = module.Structs.FirstOrDefault(s => s.Name == typeName);
                AddStruct(wgslStruct);
                return typeName;
        }
    }
    
    public static string EmitAllStructs(Dictionary<string, string> structSources)
    {
        var sb = new StringBuilder();
        sb.Clear();
        sb.Append("using System;\n");
        sb.Append("using System.Numerics;\n");
        sb.Append("\n");
        
        foreach (var kv in structSources)
        {
            var source = kv.Value;
            sb.Append(source);
        }
        return sb.ToString();
    }
}