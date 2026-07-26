// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.WGSL;


public class WgslTypeMappings
{
    public Dictionary<string, string> map    { get; set; }
    
    public static WgslType2CSharpType[] LoadTypeMapping(string path, out string error)
    {
        try {
            if (!File.Exists(path)) {
                error = null;
                return [];
            }
            using var stream = new FileStream(path, FileMode.Open);
            var mappings = JsonSerializer.Deserialize<WgslTypeMappings>(stream);
            var map = mappings.map;
            if (map == null) {
                return Error(path, "missing member: map", out error);
            }
            var list = new List<WgslType2CSharpType>(map.Count);
            
            foreach (var kv in map)
            {
                var key = kv.Key;
                if (key == null) {
                    return Error(path, "key is null", out error);
                }
                if (!Enum.TryParse<CsTypeCode>(key, out var typeCode)) {
                    return Error(path, $"Invalid wgsl type (non generic version required) type: ${key}", out error);
                }
                var type = kv.Value;
                if (type == null) {
                    return  Error(path, $"missing type at '{key}'", out error);
                }
                var lastDot = type.LastIndexOf('.');
                var className = type.Substring(lastDot + 1);
                if (!IsValidCSharpIdentifier(className)) {
                    return Error(path, $"Invalid C# type: ${type}", out error);
                }
                var @namespace = "";
                if (lastDot != -1) {
                    @namespace = type.Substring(0, lastDot);
                    if (!IsValidCSharpIdentifier(@namespace)) {
                        return Error(path, $"Invalid C# type: ${@type}", out error);
                    }
                }
                list.Add(new WgslType2CSharpType(typeCode, @namespace, className));
            }
            error = null;
            return list.ToArray();
        }
        catch (Exception exception)
        {
            var message = exception.Message.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            error = $"Loading {path} failed: {message}";
            return [];
        }
    }
    
    private static WgslType2CSharpType[] Error(string path, string message, out string error)
    {
        error = $"Failed reading '{path}' - Error: {message}";
        return [];
    }
   
    private static bool IsValidCSharpIdentifier(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }
}
