// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;


// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;


public partial class TypeMappings
{
    public const string MappingPath = "wgsl-type-map.json";
        
    public Dictionary<string, string> map    { get; set; }
    
    public static TypeMapping[] LoadTypeMappings(string path, out string error)
    {
        try {
            if (!File.Exists(path)) {
                error = null;
                return [];
            }
            return LoadJsonTypeMapping(path, out error);
        }
        catch (Exception exception)
        {
            var message = exception.Message.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            error = $"Loading {path} failed: {message}";
            return [];
        }
    }
    
    private static TypeMapping[] Error(string path, string message, out string error)
    {
        error = $"Failed reading '{path}' - Error: {message}";
        return [];
    }
   
    private static bool IsValidCSharpIdentifier(string name)
    {
        return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*(\s*<\s*[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*\s*>)?$");
    }

    private static bool IsValidCSharpNamespace(string ns)
    {
        return Regex.IsMatch(ns, @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$");
    }
}