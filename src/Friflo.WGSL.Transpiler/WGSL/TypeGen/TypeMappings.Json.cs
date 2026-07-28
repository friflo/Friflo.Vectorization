// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

#if DISABLED_JSON_SUPPORT

using System.Collections.Generic;
using System.IO;
using System.Text.Json;


// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;


/*
JSON content example:

{
  "map": {
    "vec2i": "CustomTypes.Vector2i",
    "vec2u": "CustomTypes.Vector2<uint>",
    
    "mat2x2h": "OpenTK.Mathematics.Matrix2",
    "mat2x3h": "Silk.NET.Maths.Matrix2x3<Half>",
    "mat2x4h": "Unity.Mathematics.float2x4"
  }
}

*/
public partial class TypeMappings
{
    public Dictionary<string, string> map    { get; set; }
    
    private static TypeMapping[] LoadJsonTypeMapping(string path, out string error)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var mappings = JsonSerializer.Deserialize<TypeMappings>(stream);
        var map = mappings?.map;
        if (map == null) {
            return FileError(path, "missing member: map", out error);
        }
        var list = new List<TypeMapping>(map.Count);
        
        foreach (var kv in map)
        {
            var key     = kv.Key;
            var type    = kv.Value;
            var mapping = GetTypeMapping(key, type, out error);
            if (error != null) {
                return FileError(path, error, out error);
            }
            list.Add(mapping);
        }
        error = null;
        return list.ToArray();
    }
}

#endif
