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
        var type                    = GetIdentifier(typeSymbol);
        var (size, align, typeCode) = GetTypeCode(typeSymbol);
        var isValueType = typeSymbol.IsValueType;
        
        if (CsTypeCode.None != typeCode || !isValueType)
        {
            typeInfo = new CsTypeInfo {
                Identifier  = type,
                TypeLayout  = new CsTypeLayout(size, align),
                Fields      = default,
                TypeCode    = typeCode
            };
            semanticInfo.types.Add(typeSymbol, typeInfo);
            return typeInfo;
        }

        ValueArray<CsField> fields = default;
        int  structSize     = 0;
        int  maxAlign       = 1;
        
        if (isValueType && typeSymbol is INamedTypeSymbol structSymbol)
        {
            var typeLayout  = semanticInfo.GetTypeLayout(typeSymbol);
            structSize      = typeLayout.Size;
            var calcSize    = typeLayout.Size == 0;

            // recursion only for struct types
            var fieldSymbols = structSymbol.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(fieldSymbol => !fieldSymbol.IsStatic);
            var fieldList = new List<CsField>();
            foreach (var fieldSymbol in fieldSymbols)
            {
                var fieldTypeInfo = GetTypeInfo(semanticInfo, fieldSymbol.Type); // recursive call
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
                if (!calcSize) {
                    continue;
                }
                var fieldLayout = fieldTypeInfo.TypeLayout;
                maxAlign        = Math.Max(maxAlign, fieldLayout.Align);
                structSize     += fieldLayout.Size;
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
            TypeLayout  = new CsTypeLayout(structSize, maxAlign),
            Fields      = fields,
            TypeCode    = typeCode
        };
        semanticInfo.types.Add(typeSymbol, typeInfo);
        return typeInfo;
    }
    
    
    private static (int size, int align, CsTypeCode) GetTypeCode(ITypeSymbol symbol)
    {
        (int size, int align, CsTypeCode typeCode) = symbol.SpecialType switch {
            SpecialType.System_Object 	    => (0, 0, CsTypeCode.Object),
            SpecialType.System_Enum 	    => (0, 0, CsTypeCode.Enum),
            SpecialType.System_ValueType    => (0, 0, CsTypeCode.ValueType),
            SpecialType.System_Boolean 	    => (0, 0, CsTypeCode.Bool),         // WGSL type (not in buffers)
            SpecialType.System_Char 	    => (0, 0, CsTypeCode.Char),
            //
            SpecialType.System_SByte 	    => (1, 1, CsTypeCode.SByte),
            SpecialType.System_Byte 	    => (1, 1, CsTypeCode.Byte),
            SpecialType.System_Int16 	    => (2, 2, CsTypeCode.Int16),
            SpecialType.System_UInt16 	    => (2, 2, CsTypeCode.UInt16),
            SpecialType.System_Int32 	    => (4, 4, CsTypeCode.i32),          // WGSL type
            SpecialType.System_UInt32	    => (4, 4, CsTypeCode.u32),          // WGSL type
            SpecialType.System_Int64 	    => (8, 8, CsTypeCode.Int64),
            SpecialType.System_UInt64 	    => (8, 8, CsTypeCode.UInt64),
            SpecialType.System_Single 	    => (4, 4, CsTypeCode.f32),          // WGSL type
            SpecialType.System_Double 	    => (8, 8, CsTypeCode.Double),
            //
            SpecialType.System_Decimal	    => (0, 0, CsTypeCode.Decimal),
            SpecialType.System_String 	    => (0, 0, CsTypeCode.String),
            SpecialType.System_DateTime     => (0, 0, CsTypeCode.DateTime),
            _                               => (0, 0, CsTypeCode.None)
        };
        if (typeCode != CsTypeCode.None) {
            return (size, align, typeCode);
        }
        var ns          = GetNamespace(symbol);
        var symbolName  = symbol.Name;
        switch (ns)
        {
            case "System":
                return symbolName switch {
                    "Half"                  => (2, 2, CsTypeCode.f16),         // WGSL type
                    "Span"                  => (0, 0, CsTypeCode.Span),
                    "ReadOnlySpan"          => (0, 0, CsTypeCode.ReadOnlySpan),
                    _                       => (0, 0, CsTypeCode.None)
                };
            case "System.Numerics":
                return symbolName switch {
                    "Vector2"               => ( 8, 4, CsTypeCode.vec2f),       // WGSL type
                    "Vector3"               => (12, 4, CsTypeCode.vec3f),       // WGSL type
                    "Vector4"               => (16, 4, CsTypeCode.vec4f),       // WGSL type
                    "Matrix3x2"             => (24, 4, CsTypeCode.mat3x2f),     // WGSL type
                    "Matrix4x4"             => (64, 4, CsTypeCode.mat4x4f),     // WGSL type
                    _                       => ( 0, 0, CsTypeCode.None)
                };
            case "Friflo.Vectorization.GPU":
                return symbolName switch {
                    "InBuffer"              => (0, 0, CsTypeCode.InBuffer),
                    "InOutBuffer"           => (0, 0, CsTypeCode.InOutBuffer),
                    _                       => (0, 0, CsTypeCode.None)
                };
            case "Friflo.Vectorization.WebGPU":
                return symbolName switch {
                    "GpuSampler"            => (0, 0, CsTypeCode.GpuSampler),
                    "GpuTextureView"        => (0, 0, CsTypeCode.GpuTextureView),
                    _                       => (0, 0, CsTypeCode.None)
                };
            default:
                return (0, 0, CsTypeCode.None);
        }
    }
}
