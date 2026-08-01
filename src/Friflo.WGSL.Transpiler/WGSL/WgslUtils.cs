// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
namespace Friflo.WGSL.Transpiler.WGSL;


public static class DictionaryExtensions
{
    public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.ContainsKey(key))
        {
            return false;
        }

        dictionary.Add(key, value);
        return true;
    }
}

public static class WgslUtils
{
    public static ImmutableDictionary<string, string> CreateDictionary(ImmutableArray<WgslFile> wgslFiles, string projDir, ValueArray<CsShader> shaders)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();

        builder.Add("wgsl_length", wgslFiles.Length.ToString());
        for (int i = 0; i < wgslFiles.Length; i++)
        {
            builder.Add($"wgsl_content_{i}", wgslFiles[i].Content);
            builder.Add($"wgsl_path_{i}",    wgslFiles[i].NormalizedPath);
        }
        if (projDir != null) {
            builder.Add("proj_dir", projDir);
        }
        if (shaders.Length > 0) {
            var shaderStrings = shaders.Select(s => s.path);
            var shadersJoined = string.Join("|", shaderStrings);
            builder.Add("shader_files", shadersJoined);
        }
        return builder.ToImmutable();
    }
    
    public static WgslFile[] CreateWgslFiles(ImmutableDictionary<string, string> properties)
    {
        var list = new List<WgslFile>();

        if (properties.TryGetValue("wgsl_length", out var lengthStr) && 
            int.TryParse(lengthStr, out int length))
        {
            for (int i = 0; i < length; i++)
            {
                var contentStr = properties.TryGetValue($"wgsl_content_{i}", out var c) ? (c ?? string.Empty) : string.Empty;
                var pathStr    = properties.TryGetValue($"wgsl_path_{i}",    out var p) ? (p ?? string.Empty) : string.Empty;

                list.Add(new WgslFile { 
                    Content         = contentStr, 
                    NormalizedPath  = pathStr,
                    Hash            = 0,
                    Module          = null
                });
            }
        }
        return list.ToArray();
    }
    
    public static string GetExceptionAsString(Exception exception)
    {
        var text = exception.ToString();
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None);
        var sb = new StringBuilder();
        if (lines.Length > 0) {
            sb.Append($"{lines[0]}\n");
        }
        for (int n = 1; n < lines.Length; n++)
        {
            var line = lines[n];
            var last = line.LastIndexOf(')');
            if (last == -1) {
                sb.Append(line);
            } else {
                sb.Append(line.Substring(0, last + 1));
            }
            sb.Append("\n");
        }
        if (sb.Length > 0) sb.Length -=1;
        return sb.ToString();
    }
    
    public static WgslFile[] LoadShaderFilesRecursive(string srcFolder)
    {
        var folder  = Path.GetFullPath(srcFolder);
        if (!Directory.Exists(folder)) {
            throw new InvalidOperationException($"folder not found: {folder}  CurrentDirectory: {Environment.CurrentDirectory}");
        } 
        var fullBaseDir = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var list = new List<WgslFile>();

        // iterate recursive all *.wgsl files
        foreach (var fullFilePath in Directory.EnumerateFiles(fullBaseDir, "*.wgsl", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(fullFilePath);
            var normalizedPath = fullFilePath.Replace('\\','/');
            list.Add(new WgslFile{ NormalizedPath = normalizedPath, Content = content, Hash =  0, Module = null });
        }
        return list.ToArray();
    }
}