// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;
using static Friflo.WGSL.Transpiler.WGSL.TypeResolution;

// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable UnusedMember.Local
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;


public sealed partial class TypeGen
{
    private readonly    StringBuilder   fileBuilder             = new ();
    private readonly    StringBuilder   body                    = new();
    //
    private readonly    HashSet<string> additionalNamespaces    = [];
    
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
    
    private static string PathToNamespace(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return "global";

        var parts = dir.Split(['/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            var rest = p.Length > 1 ? p.Substring(1) : "";
            parts[i] = (char.IsDigit(p[0]) ? "_" : "") + char.ToUpperInvariant(p[0]) + rest;
        }
        return $"{string.Join(".", parts)}";
    }
    
    private static void MapType(CSharpIdentifier[] typeCodeMap, CsTypeCode code, string ns, string typeName, TypeResolution resolution) {
        typeCodeMap[(int)code] = new CSharpIdentifier(typeName, ns, resolution);
    }
    
    private static CSharpIdentifier[] CreateTypeMap(TypeMapping[] mappings)
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var map     = new CSharpIdentifier[length];
        var values  = Enum.GetValues(typeof(CsTypeCode)).Cast<CsTypeCode>();
        
        foreach (var value in values) {
            if ((int)value >= length) continue;
            MapType(map, value, "", value.ToString(), Unmapped);
        }
        MapType(map, CsTypeCode.f16,     "",                "Half",        Resolved);
        MapType(map, CsTypeCode.f32,     "",                "float",       Resolved);
        MapType(map, CsTypeCode.i32,     "",                "int",         Resolved);
        MapType(map, CsTypeCode.u32,     "",                "uint",        Resolved);
        
        MapType(map, CsTypeCode.vec2f,   "System.Numerics", "Vector2",     Resolved);
        MapType(map, CsTypeCode.vec3f,   "System.Numerics", "Vector3",     Resolved);
        MapType(map, CsTypeCode.vec4f,   "System.Numerics", "Vector4",     Resolved);
        
        MapType(map, CsTypeCode.mat4x4f, "System.Numerics", "Matrix4x4",   Resolved);
        MapType(map, CsTypeCode.mat3x2f, "System.Numerics", "Matrix3x2",   Resolved);

        foreach (var mapping in mappings) {
            map[(int)mapping.typeCode] = mapping.identifier;
        }
        return map;
    }
    
    private void AddNamespace(in CSharpType csharpType)
    {
        if (csharpType.identifier.Namespace == "") {
            return;
        }
        additionalNamespaces.Add(csharpType.identifier.Namespace);
    }
    
    public void EmitAllStructs(WgslFile[] wgslFiles, string projDir, TypeMapping[] mappings, ToolError[] errors)
    {
        wgslFiles = wgslFiles.ToArray();
        var errorFilePath = $"{projDir}/generator-error.cs";
        if (errors.Length == 0) {
            if (File.Exists(errorFilePath)) {
                File.Delete(errorFilePath);    
            }
        } else {
            var sb = new StringBuilder();
            foreach (var error in errors) {
                sb.Append($"#error {error}\n");
            }
            File.WriteAllText(errorFilePath, sb.ToString(), new UTF8Encoding(false));
        }
        TypeMap = CreateTypeMap(mappings);
        
        for (int n = 0; n < wgslFiles.Length; n++) {
            var path =  wgslFiles[n].NormalizedPath.Substring(projDir.Length + 1);
            wgslFiles[n] = wgslFiles[n] with{ NormalizedPath =  path };
        }
        // DebugInputs(wgslFiles, projDir);
        
        // sort for deterministic generation
        WgslFile.Sort(wgslFiles);
        var files = new List<(string, string)>();
        foreach (var file in wgslFiles)
        {
            var content = EmitFile(file);
            if (content == null) continue;
            files.Add((file.NormalizedPath, content));
        }
        UpdateFiles(projDir, files);
    }
    
    private static void UpdateFiles(string projDir, List<(string, string)> files)
    {
        var searchPath  = Path.GetFullPath(projDir);
        var currentFiles = new HashSet<string>();
        if (Directory.Exists(searchPath)) {
            var fullBaseDir = searchPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var fullFilePath in Directory.EnumerateFiles(fullBaseDir, "*.wgsl.cs", SearchOption.AllDirectories))
            {
                var normalizedPath = fullFilePath.Substring(fullBaseDir.Length + 1).Replace('\\', '/');
                var expect = $"// <auto-generated />  path: {normalizedPath}";
                using var reader = new StreamReader(fullFilePath);
                var firstLine = reader.ReadLine();
                if (firstLine == expect) {
                    currentFiles.Add(normalizedPath);
                }
            }
        }
        foreach (var (path, content) in files) {
            var absPath = $"{projDir}/{path}.cs";
            currentFiles.Remove($"{path}.cs");
            if (!File.Exists(absPath) || File.ReadAllText(absPath) != content) {
                File.WriteAllText(absPath, content, new UTF8Encoding(false));
            }
        }
        foreach (var path in currentFiles) {
            var absPath = $"{projDir}/{path}";
            File.Delete(absPath);
        }
    }
    
    private string EmitFile(WgslFile file)
    {
        var normalizedPath = file.NormalizedPath;
        try {
            // --- clear state first!
            fileBuilder.Clear();
            body.Clear();
            localStructs.Clear();
            requiredStructs.Clear();
            emittedStructs.Clear();
            wgslStructs.Clear();
            fixedSizedArrays.Clear();
            additionalNamespaces.Clear();
            fileNamespace = PathToNamespace(normalizedPath);
            
            // --- process after
            fileBuilder.Append($"// <auto-generated />  path: {normalizedPath}.cs\n");
            module = FastWgslParser.ParseWgsl(file.Content, normalizedPath);
            EmitStructs(body, normalizedPath);
            if (body.Length == 0) {
                return null;
            }
            fileBuilder.Append( // language=csharp
                """
                using System;
                using System.Diagnostics;
                using System.Diagnostics.CodeAnalysis;
                using System.Runtime.CompilerServices;
                using System.Runtime.InteropServices;
                using Friflo.Vectorization.WebGPU;
                
                """);
            foreach (var ns in additionalNamespaces) {
                fileBuilder.Append($"using {ns};\n");
            }
            fileBuilder.Append( // language=csharp
                $"""
                
                namespace {fileNamespace};
                
                
                {body}{fixedSizedArrays}
                """);
        }
        catch (Exception exception) {
            fileBuilder.Append( // language=csharp
                $"""
                /* -------- Error parsing: {normalizedPath}
                {WgslUtils.GetExceptionAsString(exception)}
                */
                """);
        }
        return fileBuilder.ToString();
    }
}
