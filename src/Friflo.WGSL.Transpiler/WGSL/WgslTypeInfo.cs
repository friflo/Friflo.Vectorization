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


public readonly struct WgslTypeInfo
{
    public readonly CsTypeCode      typeCode;
    public readonly WgslParamType   paramType;
    public readonly int             arraySize;
    public readonly string          elementType; // != null if struct or typo
    
    public bool IsArray => paramType == WgslParamType.FixedSizeArray || paramType == WgslParamType.DynamicArray;
    
    private static readonly  Dictionary<string, WgslTypeInfo>  WgslTypeMap = new();
    
    static WgslTypeInfo()
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var wgslMap = WgslTypeMap;
        var values  = Enum.GetValues(typeof(CsTypeCode)).Cast<CsTypeCode>();
        
        foreach (var value in values) {
            if ((int)value >= length) continue;
            wgslMap[value.ToString()] = new WgslTypeInfo(value);
        }
    }
        
    public override string      ToString()
    {
        var typeName = typeCode == CsTypeCode.None ? elementType : typeCode.ToString();
        return paramType switch {
            WgslParamType.DynamicArray      => $"array<{typeName}>",
            WgslParamType.FixedSizeArray    => $"array<{typeName},{arraySize}>",
            _                               => typeName
        };
    }

    private WgslTypeInfo(CsTypeCode typeCode) {
        this.typeCode       = typeCode;
    }
    
    internal WgslTypeInfo(CsTypeCode typeCode, WgslParamType paramType, int arraySize, string elementType) {
        this.typeCode       = typeCode;
        this.paramType      = paramType;
        this.arraySize      = arraySize;
        this.elementType    = elementType;
    }

    
    private static WgslTypeInfo GetTypeInfo(CsTypeCode code, int offset) => new ((CsTypeCode)(int)code + offset);

    private static readonly WgslTypeInfo InvalidType = new(CsTypeCode.None);
    
    private static WgslTypeInfo GetVec(GenericArgs args, CsTypeCode code)
    {
        return args.arg_0 switch
        {
            "f16"   => GetTypeInfo(code, 0),
            "f32"   => GetTypeInfo(code, 1),
            "i32"   => GetTypeInfo(code, 2),
            "u32"   => GetTypeInfo(code, 3),
            _       => InvalidType
        };
    }
    
    private static WgslTypeInfo GetMat(GenericArgs args, int w, int h)
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
            "f16"   => GetTypeInfo(code, 0),
            "f32"   => GetTypeInfo(code, 1),
            _       => InvalidType
        };
    }
    
    private static WgslTypeInfo GetArray(GenericArgs args)
    {
        var typeInfo    = GetTypeInfo(args.arg_0, default);
        var paramType   = int.TryParse(args.arg_1, out var arraySize)
            ? WgslParamType.FixedSizeArray
            : WgslParamType.DynamicArray;
        var elementType = typeInfo.typeCode == CsTypeCode.None ? args.arg_0 : null;
        return new WgslTypeInfo(typeInfo.typeCode, paramType, arraySize, elementType);
    }

    internal static WgslTypeInfo GetTypeInfo(string name, GenericArgs args)
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
        if (WgslTypeMap.TryGetValue(name, out var typeInfo)) {
            return typeInfo;
        }
        return new WgslTypeInfo(CsTypeCode.None);
    }
}