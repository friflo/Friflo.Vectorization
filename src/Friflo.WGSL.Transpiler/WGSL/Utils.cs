// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.Immutable;

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
    public static ImmutableDictionary<string, string> CreateDictionary(List<WgslFile> wgslFiles)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();

        builder.Add("wgsl_length", wgslFiles.Count.ToString());

        for (int i = 0; i < wgslFiles.Count; i++)
        {
            builder.Add($"wgsl_content_{i}", wgslFiles[i].Content);
            builder.Add($"wgsl_path_{i}",    wgslFiles[i].NormalizedPath);
        }
        return builder.ToImmutable();
    }
    
    public static List<WgslFile> CreateWgslFiles(ImmutableDictionary<string, string> properties)
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
                    Module          = null,
                    Source          = null
                });
            }
        }
        return list;
    }
}