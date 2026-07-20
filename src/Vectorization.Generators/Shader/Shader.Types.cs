// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.WGSL.Transpiler.CSharp;
using Microsoft.CodeAnalysis;

// ReSharper disable ConvertSwitchStatementToSwitchExpression
// ReSharper disable once CheckNamespace
namespace Friflo;

    
public sealed partial class ShaderGen
{
    private static CsTypeCode GetTypeCode(ITypeSymbol symbol)
    {
        var typeCode = symbol.SpecialType switch {
            SpecialType.System_Object 	    => CsTypeCode.Object,
            SpecialType.System_Enum 	    => CsTypeCode.Enum,
            SpecialType.System_ValueType    => CsTypeCode.ValueType,
            SpecialType.System_Boolean 	    => CsTypeCode.Bool,         // WGSL type (not in buffers)
            SpecialType.System_Char 	    => CsTypeCode.Char,
            SpecialType.System_SByte 	    => CsTypeCode.i8,
            SpecialType.System_Byte 	    => CsTypeCode.u8,
            SpecialType.System_Int16 	    => CsTypeCode.i16,
            SpecialType.System_UInt16 	    => CsTypeCode.u16,
            SpecialType.System_Int32 	    => CsTypeCode.i32,          // WGSL type
            SpecialType.System_UInt32	    => CsTypeCode.u32,          // WGSL type
            SpecialType.System_Int64 	    => CsTypeCode.i64,
            SpecialType.System_UInt64 	    => CsTypeCode.u64,
            SpecialType.System_Decimal	    => CsTypeCode.Decimal,
            SpecialType.System_Single 	    => CsTypeCode.f32,          // WGSL type
            SpecialType.System_Double 	    => CsTypeCode.f64,
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
                // TODO implement duck typing - detect WGSL types from their layout: E.g. a struct with 3 float fields is a  vec3f
                
                // WGSL types not covered by BCL. Any namespace can be used
                return symbolName switch {
                    // --- vector 2, 3, 4
                    "vec2h"                 =>  CsTypeCode.vec2h,
                    "vec3h"                 =>  CsTypeCode.vec3h,
                    "vec4h"                 =>  CsTypeCode.vec4h,
                    // 
                    "vec2f"                 =>  CsTypeCode.vec2f,
                    "vec3f"                 =>  CsTypeCode.vec3f,
                    "vec4f"                 =>  CsTypeCode.vec4f,
                    //
                    "vec2i"                 =>  CsTypeCode.vec2i,
                    "vec3i"                 =>  CsTypeCode.vec3i,
                    "vec4i"                 =>  CsTypeCode.vec4i,
                    //
                    "vec2u"                 =>  CsTypeCode.vec2u,
                    "vec3u"                 =>  CsTypeCode.vec3u,
                    "vec4u"                 =>  CsTypeCode.vec4u,
                    // --- rectangular matrices
                    "mat2x3h"               =>  CsTypeCode.mat2x3h,
                    "mat2x4h"               =>  CsTypeCode.mat2x4h,
                    "mat3x2h"               =>  CsTypeCode.mat3x2h,
                    "mat3x4h"               =>  CsTypeCode.mat3x4h,
                    "mat4x2h"               =>  CsTypeCode.mat4x2h,
                    "mat4x3h"               =>  CsTypeCode.mat4x3h,
                    //
                    "mat2x3f"               =>  CsTypeCode.mat2x3f,
                    "mat2x4f"               =>  CsTypeCode.mat2x4f,
                    "mat3x2f"               =>  CsTypeCode.mat3x2f,
                    "mat3x4f"               =>  CsTypeCode.mat3x4f,
                    "mat4x2f"               =>  CsTypeCode.mat4x2f,
                    "mat4x3f"               =>  CsTypeCode.mat4x3f,
                    // --- quadratic matrices
                    "mat2x2h" or "mat2h"    =>  CsTypeCode.mat2x2h,
                    "mat3x3h" or "mat3h"    =>  CsTypeCode.mat3x3h,
                    "mat4x4h" or "mat4h"    =>  CsTypeCode.mat4x4h,
                    "mat2x2f" or "mat2f"    =>  CsTypeCode.mat2x2f,
                    "mat3x3f" or "mat3f"    =>  CsTypeCode.mat3x3f,
                    "mat4x4f" or "mat4f"    =>  CsTypeCode.mat4x4f,
                    
                    _ => CsTypeCode.None
                };
        }
    }
}
