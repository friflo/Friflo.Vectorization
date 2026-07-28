// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;


public partial class TypeMappings
{
    public Dictionary<string, string> map    { get; set; }
    
    private static TypeMapping[] LoadJsonTypeMapping(string path, out string error)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var mappings = JsonSerializer.Deserialize<TypeMappings>(stream);
        var map = mappings?.map;
        if (map == null) {
            return Error(path, "missing member: map", out error);
        }
        var list = new List<TypeMapping>(map.Count);
        
        foreach (var kv in map)
        {
            var key = kv.Key;
            if (key == null) {
                return Error(path, "key is null", out error);
            }
            if (!Enum.TryParse<CsTypeCode>(key, out var typeCode)) {
                return Error(path, $"Invalid wgsl type (non generic version required) type: {key}", out error);
            }
            var type = kv.Value;
            if (type == null) {
                return  Error(path, $"missing type at '{key}'", out error);
            }
            var lastDot = type.LastIndexOf('.');
            var className = type.Substring(lastDot + 1);
            if (!IsValidCSharpIdentifier(className)) {
                return Error(path, $"Invalid C# type: {type}", out error);
            }
            var @namespace = "";
            if (lastDot != -1) {
                @namespace = type.Substring(0, lastDot);
                if (!IsValidCSharpNamespace(@namespace)) {
                    return Error(path, $"Invalid C# namespace: {type}", out error);
                }
            }
            list.Add(new TypeMapping(typeCode, @namespace, className));
        }
        error = null;
        return list.ToArray();
    }
}