// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;
// ReSharper disable MergeIntoLogicalPattern

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.WGSL;


internal struct GenericArgs
{
    internal required string arg_0;
    internal required string arg_1;
    
    internal static GenericArgs Create(ValueArray<WgslType> generics)
    {
        var arg_0 = generics.Length > 0 ? generics[0].Name : null;
        var arg_1 = generics.Length > 1 ? generics[1].Name : null;
        return new GenericArgs { arg_0 = arg_0, arg_1 = arg_1 };
    }
}

public enum WgslParamType
{
    None,
    FixedSizeArray,
    DynamicArray
}


public readonly struct CSharpType
{
    public readonly string          typeName;
    public readonly CsTypeCode      typeCode;
    public readonly WgslParamType   paramType;
    public readonly int             arraySize;
    public readonly CSharpStruct    csharpStruct; // != null if struct

    public override string      ToString()
    {
        return paramType switch {
            WgslParamType.DynamicArray      => $"array<{typeCode}>",
            WgslParamType.FixedSizeArray    => $"array<{typeCode},{arraySize}>",
            _                               => typeCode.ToString()
        };
    }

    internal CSharpType(string typeName, CsTypeCode typeCode) {
        this.typeName       = typeName;
        this.typeCode       = typeCode;
    }
    
    private CSharpType(string typeName, CsTypeCode typeCode, WgslParamType paramType, int arraySize) {
        this.typeName       = typeName;
        this.typeCode       = typeCode;
        this.paramType      = paramType;
        this.arraySize      = arraySize;
    }
    
    internal CSharpType(string typeName, CsTypeCode typeCode, WgslParamType paramType, int arraySize, CSharpStruct csharpStruct) {
        this.typeName       = typeName;
        this.typeCode       = typeCode;
        this.paramType      = paramType;
        this.arraySize      = arraySize;
        this.csharpStruct   = csharpStruct;
    }
    
    
    private static readonly  CSharpType[]                   TypeCodeMap;
    private static readonly  Dictionary<string,CSharpType>  WgslTypeMap = new();
    
    private static void MapType(CSharpType[] typeCodeMap, Dictionary<string, CSharpType> wgslTypeMap, CsTypeCode code, string typeName) {
        typeCodeMap[(int)code]       = new CSharpType(typeName, code);
        wgslTypeMap[code.ToString()] = new CSharpType(typeName, code);
    }
    
    static CSharpType()
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var tcMap   = TypeCodeMap = new CSharpType[length];
        var wgslMap = WgslTypeMap;
        var values  = Enum.GetValues(typeof(CsTypeCode)).Cast<CsTypeCode>();
        
        foreach (var value in values) {
            if ((int)value >= length) continue;
            MapType(tcMap, wgslMap, value, value.ToString());
        }
        MapType(tcMap, wgslMap, CsTypeCode.f16,     "Half");
        MapType(tcMap, wgslMap, CsTypeCode.f32,     "float");
        MapType(tcMap, wgslMap, CsTypeCode.i32,     "int");
        MapType(tcMap, wgslMap, CsTypeCode.u32,     "uint");
        
        MapType(tcMap, wgslMap, CsTypeCode.vec2f,   "Vector2");
        MapType(tcMap, wgslMap, CsTypeCode.vec3f,   "Vector3");
        MapType(tcMap, wgslMap, CsTypeCode.vec4f,   "Vector4");
        
        MapType(tcMap, wgslMap, CsTypeCode.mat4x4f, "Matrix4x4");
        MapType(tcMap, wgslMap, CsTypeCode.mat3x2f, "Matrix3x2");
    }
    
    private static CSharpType FromCode(CsTypeCode code, int offset) => TypeCodeMap[(int)code + offset];

    private static readonly CSharpType InvalidType = new CSharpType("invalid_type", CsTypeCode.None);
    
    private static CSharpType GetVec(GenericArgs args, CsTypeCode code)
    {
        return args.arg_0 switch
        {
            "f16"   => FromCode(code, 0),
            "f32"   => FromCode(code, 1),
            "i32"   => FromCode(code, 2),
            "u32"   => FromCode(code, 3),
            _       => InvalidType
        };
    }
    
    private static CSharpType GetMat(GenericArgs args, int w, int h)
    {
        var code = (w, h) switch
        {
            (2, 2)  => CsTypeCode.mat2x2h,
            (2, 3)  => CsTypeCode.mat2x3h,
            (2, 4)  => CsTypeCode.mat2x4h,
            //
            (3, 2)  => CsTypeCode.mat3x2h,
            (3, 3)  => CsTypeCode.mat3x3h,
            (3, 4)  => CsTypeCode.mat3x4h,
            //
            (4, 2)  => CsTypeCode.mat4x2h,
            (4, 3)  => CsTypeCode.mat4x3h,
            (4, 4)  => CsTypeCode.mat4x4h,
            //
            _       => CsTypeCode.None,
        };
        if (code == CsTypeCode.None) {
            return InvalidType;
        }
        
        return args.arg_0 switch
        {
            "f16"   => FromCode(code, 0),
            "f32"   => FromCode(code, 1),
            _       => InvalidType
        };
    }
    
    private static CSharpType GetArray(GenericArgs args)
    {
        var genericType = GetCSharpType(args.arg_0, default);
        var paramType = int.TryParse(args.arg_1, out var arraySize)
            ? WgslParamType.FixedSizeArray
            : WgslParamType.DynamicArray;
        return new CSharpType(genericType.typeName, genericType.typeCode, paramType, arraySize);
    }

    internal static CSharpType GetCSharpType(string name, GenericArgs args)
    {
        switch (name)
        {
            case "array":   return GetArray(args);
            //
            case "vec2":    return GetVec(args, CsTypeCode.vec2h);
            case "vec3":    return GetVec(args, CsTypeCode.vec3h);
            case "vec4":    return GetVec(args, CsTypeCode.vec4h);
            //
            case "mat2x2":  return GetMat(args, 2, 2);
            case "mat2x3":  return GetMat(args, 2, 3);
            case "mat2x4":  return GetMat(args, 2, 4);
            //
            case "mat3x2":  return GetMat(args, 3, 2);
            case "mat3x3":  return GetMat(args, 3, 3);
            case "mat3x4":  return GetMat(args, 3, 4);
            //
            case "mat4x2":  return GetMat(args, 4, 2);
            case "mat4x3":  return GetMat(args, 4, 3);
            case "mat4x4":  return GetMat(args, 4, 4);
        }
        if (WgslTypeMap.TryGetValue(name, out var csharp)) {
            return csharp;
        }
        return new CSharpType(name, CsTypeCode.WgslStruct);
    }
}