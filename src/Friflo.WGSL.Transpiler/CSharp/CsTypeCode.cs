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
    
    Bool, // WGSL type bool not allowed on CPU - only on GPU
    
    // --- primitives type not supported by WGSL. E.g:  i8, u8, i16, u16, f64, i64, u64
    SByte, Byte,
    Int16, UInt16,
    Double, Int64, UInt64,
    //
    Enum,
    Char,
    DateTime,
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

public readonly struct TypeLayout
{
    public readonly int     size;
    public readonly int     align;

    public override string  ToString() => $"size: {size}  align: {align}";

    internal TypeLayout(int size, int align)
    {
        this.size  = size;
        this.align = align;
    }
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
        public TypeLayout Layout => TypeSizes[(int)typeCode];
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
    
    private static readonly TypeLayout[] TypeSizes;
    
    private static void SetLayout(TypeLayout[] typeSizes, CsTypeCode code, int size, int align)
    {
        typeSizes[(int)code] = new TypeLayout(size, align);
    } 
        
    static CsExtensions()
    {
        const int length = (int)CsTypeCode.WgslStruct;
        var sizes = TypeSizes = new TypeLayout [length];
        
        // --- Scalars
        SetLayout(sizes, CsTypeCode.f16, 2, 2);
        SetLayout(sizes, CsTypeCode.f32, 4, 4);
        SetLayout(sizes, CsTypeCode.i32, 4, 4);
        SetLayout(sizes, CsTypeCode.u32, 4, 4);


        // --- 2-Component Vectors
        SetLayout(sizes, CsTypeCode.vec2h,  4,  4); // 2x f16
        SetLayout(sizes, CsTypeCode.vec2f,  8,  8); // 2x f32
        SetLayout(sizes, CsTypeCode.vec2i,  8,  8); // 2x i32
        SetLayout(sizes, CsTypeCode.vec2u,  8,  8); // 2x u32

        // --- 3-Component Vectors (Payload size, buffer alignment is padded)
        SetLayout(sizes, CsTypeCode.vec3h,  6,  8); // 3x f16 (8 bytes alignment)
        SetLayout(sizes, CsTypeCode.vec3f, 12, 16); // 3x f32 (16 bytes alignment)
        SetLayout(sizes, CsTypeCode.vec3i, 12, 16); // 3x i32 (16 bytes alignment)
        SetLayout(sizes, CsTypeCode.vec3u, 12, 16); // 3x u32 (16 bytes alignment)

        // --- 4-Component Vectors
        SetLayout(sizes, CsTypeCode.vec4h,  8,  8); // 4x f16
        SetLayout(sizes, CsTypeCode.vec4f, 16, 16); // 4x f32
        SetLayout(sizes, CsTypeCode.vec4i, 16, 16); // 4x i32
        SetLayout(sizes, CsTypeCode.vec4u, 16, 16); // 4x u32


        // --- 2xN Matrices (Columns x Rows)
        SetLayout(sizes, CsTypeCode.mat2x2h,  8,  4); // 2x vec2h
        SetLayout(sizes, CsTypeCode.mat2x2f, 16,  8); // 2x vec2f

        SetLayout(sizes, CsTypeCode.mat2x3h, 16,  8); // 2x vec3h (Stride: 8)
        SetLayout(sizes, CsTypeCode.mat2x3f, 32, 16); // 2x vec3f (Stride: 16)

        SetLayout(sizes, CsTypeCode.mat2x4h, 16,  8); // 2x vec4h
        SetLayout(sizes, CsTypeCode.mat2x4f, 32, 16); // 2x vec4f

        // --- 3xN Matrices
        SetLayout(sizes, CsTypeCode.mat3x2h, 12,  4); // 3x vec2h
        SetLayout(sizes, CsTypeCode.mat3x2f, 24,  8); // 3x vec2f

        SetLayout(sizes, CsTypeCode.mat3x3h, 24,  8); // 3x vec3h (Stride: 8)
        SetLayout(sizes, CsTypeCode.mat3x3f, 48, 16); // 3x vec3f (Stride: 16)

        SetLayout(sizes, CsTypeCode.mat3x4h, 24,  8); // 3x vec4h
        SetLayout(sizes, CsTypeCode.mat3x4f, 48, 16); // 3x vec4f

        // --- 4xN Matrices
        SetLayout(sizes, CsTypeCode.mat4x2h, 16,  4); // 4x vec2h
        SetLayout(sizes, CsTypeCode.mat4x2f, 32,  8); // 4x vec2f

        SetLayout(sizes, CsTypeCode.mat4x3h, 32,  8); // 4x vec3h (Stride: 8)
        SetLayout(sizes, CsTypeCode.mat4x3f, 64, 16); // 4x vec3f (Stride: 16)

        SetLayout(sizes, CsTypeCode.mat4x4h, 32,  8); // 4x vec4h
        SetLayout(sizes, CsTypeCode.mat4x4f, 64, 16); // 4x vec4f
    }
    


}

