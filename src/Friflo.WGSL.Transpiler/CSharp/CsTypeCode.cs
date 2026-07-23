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
    
    extension (CsTypeCode typeCode)
    {
        public int ByteSize => TypeSizes[(int)typeCode];
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
    
    private static readonly int[] TypeSizes;
    
    private static void SetSize(int[] typeSizes, CsTypeCode code, int sizeInBytes)
    {
        typeSizes[(int)code] = sizeInBytes;
    } 
        
    static CsExtensions()
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var sizes = TypeSizes = new int [length];
        
        // --- Scalars
        SetSize(sizes, CsTypeCode.f16, 2);
        SetSize(sizes, CsTypeCode.f32, 4);
        SetSize(sizes, CsTypeCode.i32, 4);
        SetSize(sizes, CsTypeCode.u32, 4);

        
        // --- 2-Component Vectors
        SetSize(sizes, CsTypeCode.vec2h, 4);   // 2x f16
        SetSize(sizes, CsTypeCode.vec2f, 8);   // 2x f32
        SetSize(sizes, CsTypeCode.vec2i, 8);   // 2x i32
        SetSize(sizes, CsTypeCode.vec2u, 8);   // 2x u32

        // --- 3-Component Vectors (Payload size, buffer alignment is padded)
        SetSize(sizes, CsTypeCode.vec3h, 6);   // 3x f16 (8 bytes in buffer)
        SetSize(sizes, CsTypeCode.vec3f, 12);  // 3x f32 (16 bytes in buffer)
        SetSize(sizes, CsTypeCode.vec3i, 12);  // 3x i32 (16 bytes in buffer)
        SetSize(sizes, CsTypeCode.vec3u, 12);  // 3x u32 (16 bytes in buffer)

        // --- 4-Component Vectors
        SetSize(sizes, CsTypeCode.vec4h, 8);   // 4x f16
        SetSize(sizes, CsTypeCode.vec4f, 16);  // 4x f32
        SetSize(sizes, CsTypeCode.vec4i, 16);  // 4x i32
        SetSize(sizes, CsTypeCode.vec4u, 16);  // 4x u32

        
        // --- 2xN Matrices (Columns x Rows)
        SetSize(sizes, CsTypeCode.mat2x2h, 8);   // 2x vec2h
        SetSize(sizes, CsTypeCode.mat2x2f, 16);  // 2x vec2f

        SetSize(sizes, CsTypeCode.mat2x3h, 12);  // 2x vec3h (16 bytes in buffer)
        SetSize(sizes, CsTypeCode.mat2x3f, 24);  // 2x vec3f (32 bytes in buffer)

        SetSize(sizes, CsTypeCode.mat2x4h, 16);  // 2x vec4h
        SetSize(sizes, CsTypeCode.mat2x4f, 32);  // 2x vec4f

        // --- 3xN Matrices
        SetSize(sizes, CsTypeCode.mat3x2h, 12);  // 3x vec2h
        SetSize(sizes, CsTypeCode.mat3x2f, 24);  // 3x vec2f

        SetSize(sizes, CsTypeCode.mat3x3h, 18);  // 3x vec3h (24 bytes in buffer)
        SetSize(sizes, CsTypeCode.mat3x3f, 36);  // 3x vec3f (48 bytes in buffer)

        SetSize(sizes, CsTypeCode.mat3x4h, 24);  // 3x vec4h
        SetSize(sizes, CsTypeCode.mat3x4f, 48);  // 3x vec4f

        // --- 4xN Matrices
        SetSize(sizes, CsTypeCode.mat4x2h, 16);  // 4x vec2h
        SetSize(sizes, CsTypeCode.mat4x2f, 32);  // 4x vec2f

        SetSize(sizes, CsTypeCode.mat4x3h, 24);  // 4x vec3h (32 bytes in buffer)
        SetSize(sizes, CsTypeCode.mat4x3f, 48);  // 4x vec3f (64 bytes in buffer)

        SetSize(sizes, CsTypeCode.mat4x4h, 32);  // 4x vec4h
        SetSize(sizes, CsTypeCode.mat4x4f, 64);  // 4x vec4f
    }
    


}

