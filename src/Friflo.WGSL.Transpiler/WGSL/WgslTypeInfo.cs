// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.WGSL;


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
    public readonly string?         elementType; // != null if struct or typo
    
    public bool IsArray => paramType == WgslParamType.FixedSizeArray || paramType == WgslParamType.DynamicArray;
    
    public override string?      ToString()
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
    
    internal WgslTypeInfo(CsTypeCode typeCode, WgslParamType paramType, int arraySize, string? elementType) {
        this.typeCode       = typeCode;
        this.paramType      = paramType;
        this.arraySize      = arraySize;
        this.elementType    = elementType;
    }
    
    private static WgslTypeInfo GetTypeInfoOffset(CsTypeCode code, int offset) => new ((CsTypeCode)(int)code + offset);

    private static readonly WgslTypeInfo InvalidType = new(CsTypeCode.None);
    
    private static WgslTypeInfo GetVec(WgslTypeGenerics args, CsTypeCode code)
    {
        var info = GetTypeInfo(args.Arg_0);
        return info.typeCode switch
        {
            CsTypeCode.f16  => GetTypeInfoOffset(code, 0),
            CsTypeCode.f32  => GetTypeInfoOffset(code, 1),
            CsTypeCode.i32  => GetTypeInfoOffset(code, 2),
            CsTypeCode.u32  => GetTypeInfoOffset(code, 3),
            _               => InvalidType
        };
    }
    
    private static WgslTypeInfo GetMat(WgslTypeGenerics args, int w, int h)
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
        var info = GetTypeInfo(args.Arg_0);
        return info.typeCode switch
        {
            CsTypeCode.f16  => GetTypeInfoOffset(code, 0),
            CsTypeCode.f32  => GetTypeInfoOffset(code, 1),
            _               => InvalidType
        };
    }
    
    private static WgslTypeInfo GetArray(WgslTypeGenerics args)
    {
        var typeInfo    = GetTypeInfo(args.Arg_0);
        var paramType   = int.TryParse(args.Arg_1?.Name, out var arraySize)
            ? WgslParamType.FixedSizeArray
            : WgslParamType.DynamicArray;
        var elementType = typeInfo.typeCode == CsTypeCode.None ? args.Arg_0?.Name : null;
        return new WgslTypeInfo(typeInfo.typeCode, paramType, arraySize, elementType);
    }
    
    private static WgslTypeInfo GetType(CsTypeCode code) {
        return new WgslTypeInfo(code);
    }

    internal static WgslTypeInfo GetTypeInfo(WgslType type)
    {
        if (type == null) {
            return new WgslTypeInfo(CsTypeCode.None);
        }
        var args = type.Generics;
        
        return type.Name switch
        {
            "f16"       => GetType(CsTypeCode.f16),
            "f32"       => GetType(CsTypeCode.f32),
            "i32"       => GetType(CsTypeCode.i32),
            "u32"       => GetType(CsTypeCode.u32),
            // --- vector
            "vec2h"     => GetType(CsTypeCode.vec2h),
            "vec2f"     => GetType(CsTypeCode.vec2f),
            "vec2i"     => GetType(CsTypeCode.vec2i),
            "vec2u"     => GetType(CsTypeCode.vec2u),
            //
            "vec3h"     => GetType(CsTypeCode.vec3h),
            "vec3f"     => GetType(CsTypeCode.vec3f),
            "vec3i"     => GetType(CsTypeCode.vec3i),
            "vec3u"     => GetType(CsTypeCode.vec3u),
            //
            "vec4h"     => GetType(CsTypeCode.vec4h),
            "vec4f"     => GetType(CsTypeCode.vec4f),
            "vec4i"     => GetType(CsTypeCode.vec4i),
            "vec4u"     => GetType(CsTypeCode.vec4u),
            // --- matrix
            "mat2x2h"   => GetType(CsTypeCode.mat2x2h),
            "mat2x2f"   => GetType(CsTypeCode.mat2x2f),
            "mat2x3h"   => GetType(CsTypeCode.mat2x3h),
            "mat2x3f"   => GetType(CsTypeCode.mat2x3f),
            "mat2x4h"   => GetType(CsTypeCode.mat2x4h),
            "mat2x4f"   => GetType(CsTypeCode.mat2x4f),
            //
            "mat3x2h"   => GetType(CsTypeCode.mat3x2h),
            "mat3x2f"   => GetType(CsTypeCode.mat3x2f),
            "mat3x3h"   => GetType(CsTypeCode.mat3x3h),
            "mat3x3f"   => GetType(CsTypeCode.mat3x3f),
            "mat3x4h"   => GetType(CsTypeCode.mat3x4h),
            "mat3x4f"   => GetType(CsTypeCode.mat3x4f),
            //
            "mat4x2h"   => GetType(CsTypeCode.mat4x2h),
            "mat4x2f"   => GetType(CsTypeCode.mat4x2f),
            "mat4x3h"   => GetType(CsTypeCode.mat4x3h),
            "mat4x3f"   => GetType(CsTypeCode.mat4x3f),
            "mat4x4h"   => GetType(CsTypeCode.mat4x4h),
            "mat4x4f"   => GetType(CsTypeCode.mat4x4f),
            // --- generic
            "array"     => GetArray(args),
            //
            "vec2"      => GetVec(args, CsTypeCode.vec2h),
            "vec3"      => GetVec(args, CsTypeCode.vec3h),
            "vec4"      => GetVec(args, CsTypeCode.vec4h),
            //
            "mat2x2"    => GetMat(args, 2, 2),
            "mat2x3"    => GetMat(args, 2, 3),
            "mat2x4"    => GetMat(args, 2, 4),
            //
            "mat3x2"    => GetMat(args, 3, 2),
            "mat3x3"    => GetMat(args, 3, 3),
            "mat3x4"    => GetMat(args, 3, 4),
            //
            "mat4x2"    => GetMat(args, 4, 2),
            "mat4x3"    => GetMat(args, 4, 3),
            "mat4x4"    => GetMat(args, 4, 4),
            _           => new WgslTypeInfo(CsTypeCode.None)
        };
    }
}