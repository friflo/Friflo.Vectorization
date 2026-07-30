// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;

public readonly struct ToolError(int? line, string code, string message)
{
    public          bool    IsSet   => message != null;
    public readonly int?    line    = line;
    public readonly string  code    = code;
    public readonly string  message = message;

    public override string ToString() => IsSet ? $"'{code}': {message}" : "OK";
}

public static class TypeMappings
{
    public const string MappingPath = "wgsl-types.ini";
        
    
    public static TypeMapping[] LoadTypeMappings(string path, out ToolError error)
    {
        try {
            if (!File.Exists(path)) {
                error = new ToolError(null, "WGSL002", $"'{path}' not found.");
                return [];
            }
            return LoadPropertiesTypeMapping(path, out error);
            // return LoadJsonTypeMapping(path, out error);
        }
        catch (Exception exception)
        {
            var message = exception.Message.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            error = new ToolError(null, "WGSL001", $"Loading failed: {message}");
            return [];
        }
    }
    
    private static TypeMapping MappingError(int line, string code, string message, out ToolError error)
    {
        error = new ToolError(line, code, message);
        return default;
    }
   
    private static bool IsValidCSharpIdentifier(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*(\s*<\s*[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*\s*>)?$");
    }

    private static bool IsValidCSharpNamespace(string ns)
    {
        return Regex.IsMatch(ns, @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$");
    }
    
    private static TypeMapping GetTypeMapping(string key, string type, int line, out ToolError error)
    {
        if (key == null) {
            return MappingError(line, "WGSL010", "key is null", out error);
        }
        if (type == null) {
            return MappingError(line, "WGSL011", $"missing type at '{key}'", out error);
        }
        type = Regex.Replace(type, @"\s+", "");
        if (!Enum.TryParse<CsTypeCode>(key, out var typeCode) ||
            typeCode is CsTypeCode.None or >= CsTypeCode.WgslStruct) {
            return MappingError(line, "WGSL012", $"Invalid wgsl type (expect a concrete type or alias like: vec2i, mat3x3f, ...) was: {key}", out error);
        }
        var lastDot = type.LastIndexOf('.');
        var className = type.Substring(lastDot + 1);
        if (!IsValidCSharpIdentifier(className)) {
            return MappingError(line, "WGSL013", $"Invalid C# type: {type}", out error);
        }
        var @namespace = "";
        if (lastDot != -1) {
            @namespace = type.Substring(0, lastDot);
            if (!IsValidCSharpNamespace(@namespace)) {
                return MappingError(line, "WGSL014", $"Invalid C# namespace: {type}", out error);
            }
        }
        error = default;
        return new TypeMapping(typeCode, @namespace, className);
    }
    
    private static TypeMapping[] LoadPropertiesTypeMapping(string path, out ToolError error)
    {
        var content = File.ReadAllText(path);
        var mappings = new List<TypeMapping>();
        int pos     = 0;
        var span    = content.AsSpan();
        var length  = span.Length;
        int line    = 0;

        while (pos < length)
        {
            line++;
            while (pos < length && char.IsWhiteSpace(span[pos]) && span[pos] != '\n' && span[pos] != '\r') pos++;
            if (pos >= length) break;

            // skip comment and empty lines
            char c = span[pos];
            if (c == '#' || c == ';') {
                while (pos < length && span[pos] != '\n') pos++;
                continue;
            }
            if (c == '\r' || c == '\n') {
                pos++;
                continue;
            }

            // read key until '=' or line feed
            int keyStart = pos;
            while (pos < length && span[pos] != '=' && span[pos] != '\n' && span[pos] != '\r') {
                pos++;
            }
            if (pos >= length || span[pos] != '=') {
                continue; // found no '=' in line
            }
            var key = span.Slice(keyStart, pos - keyStart).Trim().ToString();
            pos++; // skip '='

            // read value until line feed
            int valStart = pos;
            while (pos < length && span[pos] != '\n' && span[pos] != '\r') pos++;

            var value = span.Slice(valStart, pos - valStart).Trim().ToString();

            // Consume line breaks so pos actually increments
            if (pos < length && (span[pos] == '\n' || span[pos] == '\r')) pos++;

            var mapping = GetTypeMapping(key, value, line, out error);
            if (error.IsSet) {
                return [];
            }
            mappings.Add(mapping);
        }
        error = default;
        return mappings.ToArray();
    }
}