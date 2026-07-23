// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Text;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.CSharp;



public readonly record struct SrcLoc
{
    public required     string  path     { get; init; }
    public required     int     start    { get; init; }
    public required     int     length   { get; init; }
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


// WGPU attribute:  ShaderAttribute
public readonly record struct CsShader
{
    public required                 string  path     { get; init; }
    public required                 string  vert     { get; init; }
    public required                 string  frag     { get; init; }
    //
    [Browse(Never)] public required SrcLoc  attrLoc  { get; init; }
    [Browse(Never)] public required SrcLoc  pathLoc  { get; init; }
    [Browse(Never)] public required SrcLoc  vertLoc  { get; init; }
    [Browse(Never)] public required SrcLoc  fragLoc  { get; init; }
}


// WGPU attribute:  DrawVertexIndexAttribute
public readonly record struct CsDrawVertexIndex
{
    public required     uint    vertexCount     { get; init; }
    public required     uint    instanceCount   { get; init; }
    public required     uint    firstVertex     { get; init; }
    public required     uint    firstInstance   { get; init; }
}


public record CsMethod
{
    public required     string                  Name            { get; init; }
    public required     string                  Hash            { get; init; }
    public required     ValueArray<CsShader>    Shaders         { get; init; }
    public required     CsDrawVertexIndex?      DrawVertexIndex { get; init; }
    public required     CsType                  DeclaringType   { get; init; }
    public required     ValueArray<CsParameter> Parameters      { get; init; }
    public required     ValueArray<CsTypeInfo>  TypeInfos       { get; init; }
    public required     CsModifier              Modifier        { get; init; }
    //
    [Browse(Never)] public required SrcLoc      MethodLoc       { get; init; }
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"{Name}(");
        if (Parameters.Length == 0) {
            sb.Append(")");
            return sb.ToString();
        }
        foreach (var parameter in Parameters) {
            parameter.AppendString(sb);
            sb.Append(", ");
        }
        sb.Length -= 2;
        sb.Append(")");
        return sb.ToString();
    }
}

public readonly record struct CsAttribute
{
    public required     CsTypeIdentifier            Type    { get; init; }
    public required     ValueArray<CsAttributeArg>  Args    { get; init; }
    
    public override     string                      ToString() => Type.ToString();
}

public readonly record struct CsAttributeArg
{
    public required     string  Name        { get; init; }
    public required     string  Value       { get; init; } // string or int

    public override     string  ToString()  => $"{Value} ({Name})";

    public CsAttributeArg() { }
}

public readonly record struct CsEnum {
    public required     string  Name   { get; init; }
    public required     ulong   Value  { get; init; }
    
    public override     string  ToString() => Name;
}

public readonly record struct CsAttrEnum
{
    public required     CsEnum  enum1   { get; init; } // WGSL enum:  ST,    WGPU enum:  TextureFormat
    public required     CsEnum  enum2   { get; init; } // WGSL enum:  TSA
}

public enum CsParamAttribute
{
    None,
    
    // --- GpuBuffer<>
    storage,        // WGPU attribute:  storageAttribute
    uniform,        // WGPU attribute:  uniformAttribute
    VertexBuffer,   // WGPU attribute:  VertexBufferAttribute
    IndexBuffer,    // WGPU attribute:  IndexBufferAttribute
    
    // --- GpuSampler
    sampler,                // WGPU attribute:  samplerAttribute(filtering: true)
    sampler_NonFiltering,   // WGPU attribute:  samplerAttribute(filtering: false)
    sampler_comparison,     // WGPU attribute:  sampler_comparisonAttribute
    
    // --- GpuTextureView
    texture_1d,                     // WGPU attribute:  texture_1d<ST>
    texture_2d,                     // WGPU attribute:  texture_2d<ST>
    texture_2d_array,               // WGPU attribute:  texture_2d_array<ST>
    texture_3d,                     // WGPU attribute:  texture_3d<ST>
    texture_cube,                   // WGPU attribute:  texture_cube<ST>
    texture_cube_array,             // WGPU attribute:  texture_cube_array<ST>
    //
    texture_multisampled_2d,        // WGPU attribute:  texture_multisampled_2d<ST>
    texture_depth_multisampled_2d,  // WGPU attribute:  texture_depth_multisampled_2d
    //
    texture_storage_1d,             // WGPU attribute:  texture_storage_1d<F,A>
    texture_storage_2d,             // WGPU attribute:  texture_storage_2d<F,A>
    texture_storage_2d_array,       // WGPU attribute:  texture_storage_2d_array<F,A>
    texture_storage_3d,             // WGPU attribute:  texture_storage_3d<F,A>
    //
    texture_depth_2d,               // WGPU attribute:  texture_depth_2d
    texture_depth_2d_array,         // WGPU attribute:  texture_depth_2d_array
    texture_depth_cube,             // WGPU attribute:  texture_depth_cube
    texture_depth_cube_array        // WGPU attribute:  texture_depth_cube_array
}

public enum CsDrawAttribute
{
    None,
    Draw,               // WGPU attribute:  DrawAttribute
    DrawInstance,       // WGPU attribute:  DrawInstanceAttribute
    // Index
}

public readonly record struct CsBindGroup
{
    /// <summary>Also used for slot in [VertexBuffer(slot)] </summary>
    public required                 int     group   { get; init; }
    public required                 int     binding { get; init; }
    //
    [Browse(Never)] public required SrcLoc  attrLoc { get; init; }
}

public readonly record struct CsParameter
{
    public required     string              Name            { get; init; }
    public required     CsDrawAttribute     DrawAttribute   { get; init; }
    public required     CsParamAttribute    ParamAttribute  { get; init; }
    public required     CsType              Type            { get; init; }
    public required     CsBindGroup         BindGroup       { get; init; }
    public required     int                 VertexBufferSlot{ get; init; }
    public required     CsAttrEnum          AttrEnum        { get; init; }
    //
    [Browse(Never)] public required SrcLoc  TypeLoc         { get; init; }
    [Browse(Never)] public required SrcLoc  GenericArgLoc   { get; init; }
    [Browse(Never)] public required SrcLoc  NameLoc         { get; init; }
    [Browse(Never)] public required SrcLoc  AttrLoc         { get; init; }
    [Browse(Never)] public required SrcLoc  AttrArg0Loc     { get; init; }
    [Browse(Never)] public required SrcLoc  AttrArg1Loc     { get; init; }
    
    public override     string              ToString()      => AppendString(new StringBuilder()).ToString();
    
    public bool IsBindGroupEntry    =>  ParamAttribute != CsParamAttribute.None         &&
                                        ParamAttribute != CsParamAttribute.VertexBuffer &&
                                        ParamAttribute != CsParamAttribute.IndexBuffer;
    
    public bool IsReadOnlyBuffer    => Type.TypeCode == CsTypeCode.InBuffer;
    
    public bool IsResource          => !(ParamAttribute == CsParamAttribute.uniform && !IsBuffer);
    
    public bool IsBuffer            => Type.TypeCode.IsBuffer;

    public StringBuilder AppendString(StringBuilder sb)
    {
        sb.Append(Name);
        sb.Append(" : ");
        Type.AppendString(sb);
        return sb;
    }
}

public readonly record struct CsTypeInfo
{
    public required     CsTypeIdentifier        Identifier  { get; init; }
    public required     ValueArray<CsAttribute> Attributes  { get; init; }
    public required     ValueArray<CsField>     Fields      { get; init; }
    public required     CsTypeCode              TypeCode    { get; init; }
    
    public override     string                  ToString() => Identifier.Name;
}

public readonly record struct CsField
{
    public required     ValueArray<CsAttribute> Attributes  { get; init; }
    public required     CsType                  Type        { get; init; }
    public required     string                  Name        { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Name);
        sb.Append("        : ");
        Type.AppendString(sb);
        return sb.ToString();
    }
}

public readonly record struct CsTypeIdentifier
{
    public required     string              Name        { get; init; }
    public required     string              Namespace   { get; init; }

    public override     string              ToString()  => $"{Name}";
}

public readonly record struct CsType
{
    public required     string              Name        { get; init; }
    public required     string              Namespace   { get; init; }
    public required     ValueArray<CsType>  Generics    { get; init; } // generic type arguments
    public required     bool                IsArray     { get; init; }
    public required     CsTypeCode          TypeCode    { get; init; }

    public override     string              ToString() => AppendString(new StringBuilder()).ToString();
    
    public StringBuilder AppendString(StringBuilder sb)
    {
        sb.Append(Name);
        if (Generics.Length == 0) return sb;
        sb.Append("<");
        foreach (var generic in Generics) {
            sb.Append(generic.Name);
            sb.Append(", ");
        }
        sb.Length -= 2;
        sb.Append(">");
        if (IsArray) sb.Append("[]");
        return sb;
    }
}

// --- modifier - not relevant for wgpu specific code
public readonly record struct CsModifier
{
    public required     string	                    MethodVisibility	{ get; init; }
    public required     bool                        IsMethodStatic    	{ get; init; }
    public required     bool                        IsClass             { get; init; }
    public required     ValueArray<CsParamModifier> ParamModifiers      { get; init; }
}

public readonly record struct CsParamModifier
{
    public required     string	type	{ get; init; }
}

