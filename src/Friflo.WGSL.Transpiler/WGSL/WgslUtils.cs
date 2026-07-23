// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;

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
    
    public static WgslFile[] CreateWgslFiles(ImmutableDictionary<string, string> properties, out string[] shaderFiles)
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
        shaderFiles = null;
        if (properties.TryGetValue("shader_files", out var shadersJoined)) {
            shaderFiles = shadersJoined.Split('|');
        }
        return list.ToArray();
    }
}