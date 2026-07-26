// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.WGSL.Transpiler.WGSL;


public readonly struct CSharpType
{
    public readonly CsTypeIdentifier    identifier;
    public readonly WgslTypeInfo        info;
    public readonly CSharpStruct        csharpStruct; // != null if struct
    
    public override string              ToString()  => info.ToString();

    internal CSharpType(string typeName, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = new CsTypeIdentifier(typeName);
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
        
    internal CSharpType(CsTypeIdentifier identifier, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = identifier;
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
}

public struct CSharpField
{
    public required string          name;
    public required CSharpType      type;
    public          int             offset;
    public          int             size;

    public override string          ToString() => name;
}

public class CSharpStruct
{
    public required string          name;
    public required string          source;
    public required CSharpField[]   fields;
    public required TypeLayout      layout;
    
    public override string          ToString() => name;
}

internal struct LocalStruct
{
    public required CSharpStruct    csharpStruct;
    public required bool            alreadyDeclared;
    
    public override string          ToString() => csharpStruct.name ;
}

public readonly struct WgslType2CSharpType
{
    public readonly CsTypeCode          typeCode;
    public readonly CsTypeIdentifier    identifier;
    
    public WgslType2CSharpType(CsTypeCode typeCode, string @namespace, string name)
    {
        this.typeCode   = typeCode;
        identifier = new CsTypeIdentifier(name, @namespace);
    }
}

public struct WgslTypeMapping
{
    public string   wgsl    { get; set; }
    public string   type    { get; set; }
    
    public static WgslType2CSharpType[] LoadTypeMapping(string path, out string error)
    {
        try {
            if (!File.Exists(path)) {
                error = null;
                return [];
            }
            using var stream = new FileStream(path, FileMode.Open);
            var mappings = JsonSerializer.Deserialize<WgslTypeMapping[]>(stream);
            
            var list = new List<WgslType2CSharpType>(mappings.Length);
            
            foreach (var mapping in mappings)
            {
                if (!Enum.TryParse<CsTypeCode>(mapping.wgsl, out var typeCode)) {
                    error = $"Invalid wgsl type (non generic version required) type: ${mapping.wgsl}";
                    return [];
                }
                var type = mapping.type;
                var lastDot = type.LastIndexOf('.');
                var className = type.Substring(lastDot + 1);
                if (!IsValidCSharpIdentifier(className)) {
                    error = $"Invalid C# type: ${type}";
                    return [];
                }
                var @namespace = "";
                if (lastDot != -1) {
                    @namespace = type.Substring(0, lastDot);
                    if (!IsValidCSharpIdentifier(@namespace)) {
                        error = $"Invalid C# type: ${@type}";
                        return [];
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
    
    private static bool IsValidCSharpIdentifier(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
    }
}

    

