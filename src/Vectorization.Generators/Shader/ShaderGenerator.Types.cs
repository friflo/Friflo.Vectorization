// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Vectorization.Generators.Shader;
using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;

// ReSharper disable InconsistentNaming
// ReSharper disable MergeIntoPattern
// ReSharper disable ConvertSwitchStatementToSwitchExpression
// ReSharper disable once CheckNamespace
namespace Friflo;

    
internal static partial class ShaderGenerator
{
    private static CsType GetType(SemanticInfo semanticInfo, ITypeSymbol typeSymbol, bool getFields)
    {
        if (getFields) {
            var typeInfo = GetTypeInfo(semanticInfo, typeSymbol);
            return new CsType {
                Name        = typeInfo.Identifier.Name,
                Namespace   = typeInfo.Identifier.Namespace,
                TypeCode    = typeInfo.TypeCode,
                TypeLayout  = typeInfo.TypeLayout, 
                Generics    = default,
                IsArray     = false,
            };
        }
        var type = GetIdentifier(typeSymbol);
        return new CsType {
            Name        = type.Name,
            Namespace   = type.Namespace,
            TypeCode    = CsTypeCode.None,
            TypeLayout  = default,
            Generics    = default,
            IsArray     = false
        };
    }
    
    
    private static CsTypeInfo GetTypeInfo(SemanticInfo semanticInfo, ITypeSymbol typeSymbol)
    {
        if (semanticInfo.types.TryGetValue(typeSymbol, out var typeInfo)) {
            return typeInfo;
        }
        var type        = GetIdentifier(typeSymbol);
        var typeCode    = GetTypeCode(typeSymbol);
        var isValueType = typeSymbol.IsValueType;
        
        if (CsTypeCode.None != typeCode || !isValueType)
        {
            typeInfo = new CsTypeInfo {
                Identifier  = type,
                TypeLayout  = default,
                Fields      = default,
                TypeCode    = typeCode
            };
            semanticInfo.types.Add(typeSymbol, typeInfo);
            return typeInfo;
        }

        ValueArray<CsField> fields = default;
        int structSize = 0;
        if (isValueType && typeSymbol is INamedTypeSymbol structSymbol)
        {
            var typeLayout = semanticInfo.GetTypeLayout(typeSymbol);
            if (typeLayout.Size == 0) {
                structSize = typeLayout.Size;
            }
            // recursion only for struct types
            var fieldSymbols = structSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(fieldSymbol => !fieldSymbol.IsStatic);
            var fieldList = new List<CsField>();
            foreach (var fieldSymbol in fieldSymbols)
            {
                var fieldTypeInfo   = GetTypeInfo(semanticInfo, fieldSymbol.Type); // recursive call
                fieldList.Add(new CsField {
                    Name        = fieldSymbol.Name,
                    Type        = new CsType {
                        Name        = fieldTypeInfo.Identifier.Name,
                        Namespace   = fieldTypeInfo.Identifier.Namespace,
                        TypeCode    = fieldTypeInfo.TypeCode,
                        TypeLayout  = fieldTypeInfo.TypeLayout,
                        Generics    = default,
                        IsArray     = false
                    }
                });
                if (typeLayout.Size == 0) {
                    structSize += fieldTypeInfo.TypeLayout.Size;
                }
            }
            fields = fieldList.ToValueArray();
        }
        if (fields.Length == 0) {
            typeCode = CsTypeCode.CSharpStruct;
        } else {
            typeCode = CsTypeCode.WgslStruct;
            foreach (var field in fields) {
                var fieldTypeCode = field.Type.TypeCode;
                if (fieldTypeCode.IsWgslType) {
                    continue;
                }
                typeCode = CsTypeCode.CSharpStruct;
                break;
            }
        }
        typeInfo = new CsTypeInfo {
            Identifier  = type,
            TypeLayout  = new CsTypeLayout(structSize, 0),
            Fields      = fields,
            TypeCode    = typeCode
        };
        semanticInfo.types.Add(typeSymbol, typeInfo);
        return typeInfo;
    }
    
    
    private static CsTypeCode GetTypeCode(ITypeSymbol symbol)
    {
        var typeCode = symbol.SpecialType switch {
            SpecialType.System_Object 	    => CsTypeCode.Object,
            SpecialType.System_Enum 	    => CsTypeCode.Enum,
            SpecialType.System_ValueType    => CsTypeCode.ValueType,
            SpecialType.System_Boolean 	    => CsTypeCode.Bool,         // WGSL type (not in buffers)
            SpecialType.System_Char 	    => CsTypeCode.Char,
            SpecialType.System_SByte 	    => CsTypeCode.SByte,
            SpecialType.System_Byte 	    => CsTypeCode.Byte,
            SpecialType.System_Int16 	    => CsTypeCode.Int16,
            SpecialType.System_UInt16 	    => CsTypeCode.UInt16,
            SpecialType.System_Int32 	    => CsTypeCode.i32,          // WGSL type
            SpecialType.System_UInt32	    => CsTypeCode.u32,          // WGSL type
            SpecialType.System_Int64 	    => CsTypeCode.Int64,
            SpecialType.System_UInt64 	    => CsTypeCode.UInt64,
            SpecialType.System_Decimal	    => CsTypeCode.Decimal,
            SpecialType.System_Single 	    => CsTypeCode.f32,          // WGSL type
            SpecialType.System_Double 	    => CsTypeCode.Double,
            SpecialType.System_String 	    => CsTypeCode.String,
            SpecialType.System_DateTime     => CsTypeCode.DateTime,
            _                               => CsTypeCode.None
        };
        if (typeCode != CsTypeCode.None) {
            return typeCode;
        }
        var ns          = GetNamespace(symbol);
        var symbolName  = symbol.Name;
        switch (ns)
        {
            case "System":
                return symbolName switch {
                    "Half"                  =>  CsTypeCode.f16,         // WGSL type
                    "Span"                  =>  CsTypeCode.Span,
                    "ReadOnlySpan"          =>  CsTypeCode.ReadOnlySpan,
                    _ => CsTypeCode.None
                };
            case "System.Numerics":
                return symbolName switch {
                    "Vector2"               =>  CsTypeCode.vec2f,       // WGSL type
                    "Vector3"               =>  CsTypeCode.vec3f,       // WGSL type
                    "Vector4"               =>  CsTypeCode.vec4f,       // WGSL type
                    "Matrix3x2"             =>  CsTypeCode.mat3x2f,     // WGSL type
                    "Matrix4x4"             =>  CsTypeCode.mat4x4f,     // WGSL type
                    _                       =>  CsTypeCode.None
                };
            case "Friflo.Vectorization.GPU":
                return symbolName switch {
                    "InBuffer"              =>  CsTypeCode.InBuffer,
                    "InOutBuffer"           =>  CsTypeCode.InOutBuffer,
                    _ => CsTypeCode.None
                };
            case "Friflo.Vectorization.WebGPU":
                return symbolName switch {
                    "GpuSampler"            =>  CsTypeCode.GpuSampler,
                    "GpuTextureView"        =>  CsTypeCode.GpuTextureView,
                    _ => CsTypeCode.None
                };
            default:
                return CsTypeCode.None;
        }
    }
    
    
    // Duck typing - detect WGSL types from their layout: E.g. a struct with 3 float fields is a vec3f
    private static CsTypeCode DetectWgslPrimitiveByLayout(ITypeSymbol symbol)
    {
        if (!symbol.IsValueType || symbol.TypeKind == TypeKind.Enum) {
            return CsTypeCode.None;
        }

        var fields = symbol.GetMembers()
                           .OfType<IFieldSymbol>()
                           .Where(f => !f.IsStatic)
                           .ToArray();

        if (fields.Length == 0) {
            return CsTypeCode.None;
        }

        // Detect base type: f32, f16, i32, u32 OR vector columns (vec2f, vec3f, vec4f ...)
        var baseType = GetTypeCode(fields[0].Type);

        // Check that all fields have the same scalar/vector type
        for (int i = 1; i < fields.Length; i++) {
            if (GetTypeCode(fields[i].Type) != baseType) {
                return CsTypeCode.None;
            }
        }

        var name = symbol.Name;

        // Pattern-Matching via base type and field count
        return (baseType, fields.Length) switch
        {
            // --- Matrices composed of Vector Columns (e.g. struct Mat3x3 { vec3f c0, c1, c2; })
            (CsTypeCode.vec2f, 2) => CsTypeCode.mat2x2f,
            (CsTypeCode.vec2f, 3) => CsTypeCode.mat3x2f,
            (CsTypeCode.vec2f, 4) => CsTypeCode.mat4x2f,

            (CsTypeCode.vec3f, 2) => CsTypeCode.mat2x3f,
            (CsTypeCode.vec3f, 3) => CsTypeCode.mat3x3f,
            (CsTypeCode.vec3f, 4) => CsTypeCode.mat4x3f,

            (CsTypeCode.vec4f, 2) => CsTypeCode.mat2x4f,
            (CsTypeCode.vec4f, 3) => CsTypeCode.mat3x4f,
            (CsTypeCode.vec4f, 4) => CsTypeCode.mat4x4f,

            (CsTypeCode.vec2h, 2) => CsTypeCode.mat2x2h,
            (CsTypeCode.vec2h, 3) => CsTypeCode.mat3x2h,
            (CsTypeCode.vec2h, 4) => CsTypeCode.mat4x2h,

            (CsTypeCode.vec3h, 2) => CsTypeCode.mat2x3h,
            (CsTypeCode.vec3h, 3) => CsTypeCode.mat3x3h,
            (CsTypeCode.vec3h, 4) => CsTypeCode.mat4x3h,

            (CsTypeCode.vec4h, 2) => CsTypeCode.mat2x4h,
            (CsTypeCode.vec4h, 3) => CsTypeCode.mat3x4h,
            (CsTypeCode.vec4h, 4) => CsTypeCode.mat4x4h,

            // --- Float 32-bit (f32)
            // Vectors
            (CsTypeCode.f32, 2)  => CsTypeCode.vec2f,
            (CsTypeCode.f32, 3)  => CsTypeCode.vec3f,
            (CsTypeCode.f32, 4)  => IsMatrixName(name, fields)          ? CsTypeCode.mat2x2f : CsTypeCode.vec4f,
            // Rectangular matrices
            (CsTypeCode.f32, 6)  => IsTransposedMatrixName(name, "3x2") ? CsTypeCode.mat3x2f : CsTypeCode.mat2x3f,
            (CsTypeCode.f32, 8)  => IsTransposedMatrixName(name, "4x2") ? CsTypeCode.mat4x2f : CsTypeCode.mat2x4f,
            (CsTypeCode.f32, 12) => IsTransposedMatrixName(name, "4x3") ? CsTypeCode.mat4x3f : CsTypeCode.mat3x4f,
            // Quadratic matrices
            (CsTypeCode.f32, 9)  => CsTypeCode.mat3x3f,
            (CsTypeCode.f32, 16) => CsTypeCode.mat4x4f,

            // --- Float 16-bit (f16 / Half)
            // Vectors
            (CsTypeCode.f16, 2)  => CsTypeCode.vec2h,
            (CsTypeCode.f16, 3)  => CsTypeCode.vec3h,
            (CsTypeCode.f16, 4)  => IsMatrixName(name, fields)          ? CsTypeCode.mat2x2h : CsTypeCode.vec4h,
            // Rectangular matrices
            (CsTypeCode.f16, 6)  => IsTransposedMatrixName(name, "3x2") ? CsTypeCode.mat3x2h : CsTypeCode.mat2x3h,
            (CsTypeCode.f16, 8)  => IsTransposedMatrixName(name, "4x2") ? CsTypeCode.mat4x2h : CsTypeCode.mat2x4h,
            (CsTypeCode.f16, 12) => IsTransposedMatrixName(name, "4x3") ? CsTypeCode.mat4x3h : CsTypeCode.mat3x4h,
            // Quadratic matrices
            (CsTypeCode.f16, 9)  => CsTypeCode.mat3x3h,
            (CsTypeCode.f16, 16) => CsTypeCode.mat4x4h,

            // --- Signed Integer 32-bit (i32)
            (CsTypeCode.i32, 2)  => CsTypeCode.vec2i,
            (CsTypeCode.i32, 3)  => CsTypeCode.vec3i,
            (CsTypeCode.i32, 4)  => CsTypeCode.vec4i,

            // --- Unsigned Integer 32-bit (u32)
            (CsTypeCode.u32, 2)  => CsTypeCode.vec2u,
            (CsTypeCode.u32, 3)  => CsTypeCode.vec3u,
            (CsTypeCode.u32, 4)  => CsTypeCode.vec4u,

            _                    => CsTypeCode.None
        };
    }

    private static bool IsMatrixName(string name, IFieldSymbol[] fields)
    {
        if (name.IndexOf("mat",    StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("matrix", StringComparison.OrdinalIgnoreCase) >= 0) {
            return true;
        }

        foreach (var f in fields) {
            var fieldName = f.Name;
            if (fieldName.IndexOf   ("mat",    StringComparison.OrdinalIgnoreCase) >= 0 ||
                fieldName.IndexOf   ("matrix", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fieldName.StartsWith("m",      StringComparison.OrdinalIgnoreCase))     // e.g. m11, m12, m21, m22
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTransposedMatrixName(string name, string targetDimension)
    {
        return name.IndexOf(targetDimension, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
