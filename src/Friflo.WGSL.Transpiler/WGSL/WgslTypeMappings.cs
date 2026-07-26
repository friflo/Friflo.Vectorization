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

public struct WgslTypeMapping
{
    public string   wgsl    { get; set; }
    public string   type    { get; set; }
}

public class WgslTypeMappings
{
    public WgslTypeMapping[] map    { get; set; } = [];
    
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
                return MissingMember("map", out error);
            }
            var list = new List<WgslType2CSharpType>(map.Length);
            
            foreach (var mapping in map)
            {
                if (mapping.wgsl == null) {
                    return MissingMember("wgsl", out error);
                }
                if (!Enum.TryParse<CsTypeCode>(mapping.wgsl, out var typeCode)) {
                    return Error($"Invalid wgsl type (non generic version required) type: ${mapping.wgsl}", out error);
                }
                var type = mapping.type;
                if (type == null) {
                    return MissingMember("type", out error);
                }
                var lastDot = type.LastIndexOf('.');
                var className = type.Substring(lastDot + 1);
                if (!IsValidCSharpIdentifier(className)) {
                    return Error($"Invalid C# type: ${type}", out error);
                }
                var @namespace = "";
                if (lastDot != -1) {
                    @namespace = type.Substring(0, lastDot);
                    if (!IsValidCSharpIdentifier(@namespace)) {
                        return Error($"Invalid C# type: ${@type}", out error);
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
    
    private static WgslType2CSharpType[] Error(string message, out string error)
    {
        error = message;
        return [];
    }
    
    private static WgslType2CSharpType[] MissingMember(string member, out string error)
    {
        error = $"missing member: ${member}";
        return [];
    }
    
    private static bool IsValidCSharpIdentifier(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }
}
