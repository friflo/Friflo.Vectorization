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
    private         bool    IsSet   => message != null;
    public readonly int?    line    = line;
    public readonly string  code    = code;
    public readonly string  message = message;

    public override string ToString() => IsSet ? $"'{code}': {message}" : "OK";
}



public static class TypeMappings
{
    public const string MappingPath = "wgsl-types.ini";
        
#if FILE_IO
    public static TypeMapping[] LoadTypeMappings(string path, out ToolError[] errors)
    {
        try {
            if (!File.Exists(path)) {
                errors = [new ToolError(null, "WGSL002", $"'{path}' not found.")];
                return [];
            }
            return LoadPropertiesTypeMapping(path, out errors);
            // return LoadJsonTypeMapping(path, out error);
        }
        catch (Exception exception)
        {
            var message = exception.Message.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            errors = [new ToolError(null, "WGSL001", $"Loading failed: {message}")];
            return [];
        }
    }

    private static TypeMapping MappingError(int line, string code, string message, List<ToolError> errors)
    {
        errors.Add(new ToolError(line, code, message));
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
    
    private static TypeMapping GetTypeMapping(string key, string type, int line, List<ToolError> errors)
    {
        if (key == null) {
            return MappingError(line, "WGSL010", "key is null", errors);
        }
        if (type == null) {
            return MappingError(line, "WGSL011", $"missing type at '{key}'", errors);
        }
        type = Regex.Replace(type, @"\s+", "");
        if (!Enum.TryParse<CsTypeCode>(key, out var typeCode) ||
            typeCode is CsTypeCode.None or >= CsTypeCode.WgslStruct) {
            return MappingError(line, "WGSL012", $"Invalid wgsl type (expect a concrete type or alias like: vec2i, mat3x3f, ...) was: {key}", errors);
        }
        var lastDot = type.LastIndexOf('.');
        var className = type.Substring(lastDot + 1);
        if (!IsValidCSharpIdentifier(className)) {
            return MappingError(line, "WGSL013", $"Invalid C# type: {type}", errors);
        }
        var @namespace = "";
        if (lastDot != -1) {
            @namespace = type.Substring(0, lastDot);
            if (!IsValidCSharpNamespace(@namespace)) {
                return MappingError(line, "WGSL014", $"Invalid C# namespace: {type}", errors);
            }
        }
        return new TypeMapping(typeCode, @namespace, className);
    }
    
    private static TypeMapping[] LoadPropertiesTypeMapping(string path, out ToolError[] errors)
    {
        var content     = File.ReadAllText(path);
        content = content.Replace("\r\n", "\n").Replace('\r', '\n');
        var errorList   = new List<ToolError>();
        var mappings    = new List<TypeMapping>();
        int pos     = 0;
        var span    = content.AsSpan();
        var length  = span.Length;
        int line    = 0;

        while (pos < length)
        {
            line++;
            while (pos < length && char.IsWhiteSpace(span[pos]) && span[pos] != '\n') pos++;
            if (pos >= length) break;

            // skip comment and empty lines
            char c = span[pos];
            if (c == '#' || c == ';') {
                while (pos < length && span[pos] != '\n') pos++;
                continue;
            }
            if (c == '\n') {
                pos++;
                continue;
            }

            // read key until '=' or line feed
            int keyStart = pos;
            while (pos < length && span[pos] != '=' && span[pos] != '\n') {
                pos++;
            }
            if (pos >= length || span[pos] != '=') {
                pos++;
                MappingError(line, "WGSL020", $"missing type assignment '='", errorList);
                continue;
            }
            var key = span.Slice(keyStart, pos - keyStart).Trim().ToString();
            pos++; // skip '='

            // read value until line feed
            int valStart = pos;
            while (pos < length && span[pos] != '\n') pos++;

            var value = span.Slice(valStart, pos - valStart).Trim().ToString();

            // Consume line breaks so pos actually increments
            if (pos < length && (span[pos] == '\n')) pos++;

            var mapping = GetTypeMapping(key, value, line, errorList);
            if (mapping.typeCode != CsTypeCode.None) {
                mappings.Add(mapping);
            }
        }
        errors = errorList.ToArray();
        return mappings.ToArray();
    }
#endif

}

