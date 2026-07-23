// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.CSharp;

public enum CsTypeCode
{
    None,
    // --- WGSL Types
    f16, f32, i32, u32,
    
    vec2h, vec2f, vec2i, vec2u,
    vec3h, vec3f, vec3i, vec3u,
    vec4h, vec4f, vec4i, vec4u,
    
    mat2x2h, mat2x2f,
    mat2x3h, mat2x3f,
    mat2x4h, mat2x4f,

    mat3x2h, mat3x2f,
    mat3x3h, mat3x3f,
    mat3x4h, mat3x4f,

    mat4x2h, mat4x2f,
    mat4x3h, mat4x3f,
    mat4x4h, mat4x4f,
    
    WgslStruct,     // Last WGSL Type
    
    // --- non-WGSL Types
    CSharpStruct,   // A struct that cant be mapped to a WgslStruct.  Must be direct successor of WgslStruct
    Bool,           // Info: bool is part of WGSL (only on GPU)
    Enum,
    Char,
    DateTime,
    i8,  u8,
    i16, u16,
    i64, u64,
    f64,
    Decimal,
    String,
    Object,
    ValueType,
    Span,           // generic
    ReadOnlySpan,   // generic
    InBuffer,       // generic
    InOutBuffer,    // generic    
    GpuSampler,
    GpuTextureView
}


public static class CsExtensions
{
    extension (CsTypeCode typeCode)
    {
        public bool IsWgslType => typeCode is > CsTypeCode.None and <= CsTypeCode.WgslStruct;
    }
    
    extension (CsTypeCode typeCode)
    {
        public bool IsBuffer   => typeCode is CsTypeCode.InBuffer or CsTypeCode.InOutBuffer; 
    }

    extension (ValueArray<CsTypeInfo> typeInfos)
    {
        public CsTypeInfo FindTypeInfo(string @namespace, string name)
        {
            foreach (var typeInfo in typeInfos) {
                if (typeInfo.Identifier.Name == name &&  typeInfo.Identifier.Namespace == @namespace) {
                    return typeInfo;
                }
            }
            return default;
        } 
    }
}

