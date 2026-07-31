// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;
using static Friflo.WGSL.Transpiler.WGSL.TypeResolution;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;



public readonly struct TypeMapping
{
    public readonly CsTypeCode          typeCode;
    public readonly CSharpIdentifier    identifier;

    public override string ToString() => $"{typeCode} - {identifier}";

    public TypeMapping(CsTypeCode typeCode, string @namespace, string name)
    {
        this.typeCode   = typeCode;
        identifier 		= new CSharpIdentifier(name, @namespace, TypeResolution.Resolved);
    }
    
    private static void MapType(CSharpIdentifier[] typeCodeMap, CsTypeCode code, string ns, string typeName, TypeResolution resolution) {
        typeCodeMap[(int)code] = new CSharpIdentifier(typeName, ns, resolution);
    }
    
    internal static CSharpIdentifier[] CreateTypeMap(TypeMapping[] mappings)
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var map     = new CSharpIdentifier[length];
        var values  = Enum.GetValues(typeof(CsTypeCode)).Cast<CsTypeCode>();
        
        foreach (var value in values) {
            if ((int)value >= length) continue;
            MapType(map, value, "", value.ToString(), Unmapped);
        }
        MapType(map, CsTypeCode.f16,     "System",          "Half",        Resolved);
        MapType(map, CsTypeCode.f32,     "System",          "float",       Resolved);
        MapType(map, CsTypeCode.i32,     "System",          "int",         Resolved);
        MapType(map, CsTypeCode.u32,     "System",          "uint",        Resolved);
        
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
}

