// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// ReSharper disable UnusedMember.Local
// ReSharper disable CanSimplifyDictionaryLookupWithTryGetValue
// ReSharper disable InlineTemporaryVariable
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.WGSL;


internal struct StructCode {
    public string source;
    public bool   alreadyDeclared;
}

internal struct FileEntry : IComparable<FileEntry>
{
    internal    string[]    path;       // priority 1 small Length,   priority 3  element Alphabetical
    internal    bool        isCommon;   // priority 2 (true)
    internal    WgslFile    file;

    public override string  ToString() => file.NormalizedPath;

    public int CompareTo(FileEntry other)
    {
        int cmp = path.Length.CompareTo(other.path.Length);
        if (cmp != 0) return cmp;

        cmp = other.isCommon.CompareTo(isCommon);
        if (cmp != 0) return cmp;

        for (int i = 0; i < path.Length; i++) {
            cmp = string.Compare(path[i], other.path[i], StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
        }
        return 0;
    }
}

public sealed class TypeEmitter
{
    private readonly    Dictionary<string, string>  structMap   = new();
    private readonly    List<StructCode>            fileStructs = new();
    private             WgslModule                  module;
    private             string                      fileNamespace;

    
    private static void DebugInputs(WgslFile[] wgslFiles, string projDir)
    {
        var path = Path.Combine(projDir, "debug.txt");
        var sb = new StringBuilder();
        sb.Append($"projDir: {projDir}\n\n");
        
        foreach (var file in wgslFiles) {
            sb.Append($"{file.NormalizedPath}\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
    
    
    private static string PathToNamespace(string path, string root = "")
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return root;

        var parts = dir.Split(['/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            var rest = p.Length > 1 ? p.Substring(1) : "";
            parts[i] = (char.IsDigit(p[0]) ? "_" : "") + char.ToUpperInvariant(p[0]) + rest;
        }
        return $"{root}{string.Join(".", parts)}";
    }
    
    private static bool IsCommon(string path)
    {
        return path.Contains("common", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("shared", StringComparison.OrdinalIgnoreCase);
    }
    
    public void EmitAllStructs(WgslFile[] wgslFiles, string projDir)
    {
        for (int n = 0; n < wgslFiles.Length; n++) {
            var path =  wgslFiles[n].NormalizedPath.Substring(projDir.Length + 1);
            wgslFiles[n] = wgslFiles[n] with{ NormalizedPath =  path };
        }
        // DebugInputs(wgslFiles, projDir);
        
        var entries = new FileEntry[wgslFiles.Length];
        for (int n = 0; n < wgslFiles.Length; n++) {
            var file = wgslFiles[n];
            entries[n] = new FileEntry {
                path        = file.NormalizedPath.Split('/'),
                isCommon    = IsCommon(file.NormalizedPath),
                file        = file 
            };
        }
        
        // sort for deterministic generation
        Array.Sort(entries);
        
        var sb = new StringBuilder();
        
        foreach (var entry in entries)
        {
            var file = entry.file;
            var normalizedPath = file.NormalizedPath;
            try {
                // --- clear state first!
                sb.Clear();
                fileStructs.Clear();
                fileNamespace = PathToNamespace(normalizedPath);
                
                // --- process after
                module = WgslParser.ParseWgsl(file.Content, normalizedPath);
                
                EmitModule();
                
                if (fileStructs.Count == 0) {
                    continue;
                }
                sb.Append("using System;\n");
                sb.Append("using System.Numerics;\n");
                sb.Append("using Friflo.Vectorization.WebGPU;\n");
                sb.Append("\n");
                sb.Append("// ReSharper disable CheckNamespace\n");
                sb.Append("// ReSharper disable InconsistentNaming\n");
                sb.Append($"namespace {fileNamespace};\n");
                sb.Append("\n\n");
                foreach (var structSource in fileStructs) {
                    if (!structSource.alreadyDeclared) {
                        sb.Append($"[Source(\"~/{normalizedPath}\")]\n");
                    } 
                    sb.Append(structSource.source);
                }
            }
            catch (Exception exception) {
                sb.Append($"/* -------- Error parsing: {normalizedPath}\n");
                sb.Append(exception);
                sb.Append("\n*/\n");
            }
            var source =  sb.ToString();
            var path = $"{projDir}/{file.NormalizedPath}.cs";

            File.WriteAllText(path, source, new UTF8Encoding(false));
        }
    }
        
    private void EmitModule()
    {
        var structs = module.Structs;
        var bindings = module.Bindings;
        if (bindings.Count == 0 || structs.Count == 0) {
            return;
        }
        foreach (var binding in bindings)
        {
            var typeName = binding.WgslType.Name;
            var wgslStruct = structs.FirstOrDefault(s => s.Name == typeName);
            if (wgslStruct == null) continue;
            AddStruct(wgslStruct);
        }
    }
    
    private void AddStruct(WgslStruct wgslStruct)
    {
        var sb = new StringBuilder();
        sb.Clear();
        sb.Append($"public struct {wgslStruct.Name} (\n");
        foreach (var field in wgslStruct.Fields) {
            var csharpType = GetCSharpType(field.WgslType);
            sb.Append($"    {csharpType} {field.Name},\n");
        }
        sb.Length -= 2;
        sb.Append(")\n");
        sb.Append("{\n");
        foreach (var field in wgslStruct.Fields) {
            var csharpType = GetCSharpType(field.WgslType);
            sb.Append($"    public {csharpType} {field.Name} = {field.Name};\n");
        }
        sb.Append("}\n\n");
        var source          = sb.ToString();
        var alreadyDeclared = false;
        var fullQualifiedName   = $"{fileNamespace}-{wgslStruct.Name}";
        
        if (structMap.TryGetValue(fullQualifiedName, out var curSource)) {
            if (source == curSource) {
                source = $"/// Skipped identical duplicate of  <see cref=\"{wgslStruct.Name}\"/>\nfile partial class _info;\n\n";
                alreadyDeclared = true;
            }
        } else {
            structMap.Add(fullQualifiedName, source);
        }
        fileStructs.Add(new StructCode { source = source, alreadyDeclared = alreadyDeclared });
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
}