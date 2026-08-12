// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using static System.Diagnostics.DebuggerBrowsableState;
using Browse = System.Diagnostics.DebuggerBrowsableAttribute;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable InconsistentNaming
namespace Friflo.WGSL.Transpiler.CSharp;


public readonly record struct SrcLoc
{
    public required     string  path     { get; init; }
    public required     int     start    { get; init; }
    public required     int     length   { get; init; }
}


// WGPU attribute:  ShaderAttribute
public readonly record struct CsShader
{
    public required                 string  path     { get; init; }
    public required                 string  vert     { get; init; }
    public required                 string  frag     { get; init; }
    public required                 string  compute  { get; init; }
    //
    [Browse(Never)] public required SrcLoc  attrLoc     { get; init; }
    [Browse(Never)] public required SrcLoc  pathLoc     { get; init; }
    [Browse(Never)] public required SrcLoc  vertLoc     { get; init; }
    [Browse(Never)] public required SrcLoc  fragLoc     { get; init; }
    [Browse(Never)] public required SrcLoc  computeLoc  { get; init; }
}


public record CsMethod
{
    public required     string                  Name            { get; init; }
    public required     string                  Hash            { get; init; }
    public required     ValueArray<CsShader>    Shaders         { get; init; }
    public required     CsType                  DeclaringType   { get; init; }
    public required     ValueArray<CsParameter> Parameters      { get; init; }
    public required     ValueArray<CsTypeInfo>  TypeInfos       { get; init; }
    public required     CsModifier              Modifier        { get; init; }
    public required     CsWorkgroupSize?        WorkgroupSize   { get; init; }
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

public readonly record struct CsEnum {
    public required     string      Name   { get; init; }
    public required     ulong       Value  { get; init; }
    
    public override     string      ToString() => Name;
}

public readonly record struct CsAttrEnum
{
    public required     CsEnum      enum1   { get; init; } // WGSL enum:  ST,    WGPU enum:  TextureFormat
    public required     CsEnum      enum2   { get; init; } // WGSL enum:  TSA
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

public enum CsWorkloadAttribute
{
    None,
    Draw,               // WGPU attribute:  DrawAttribute
    DrawInstance,       // WGPU attribute:  DrawInstanceAttribute
    Dispatch,           // WGPU attribute:  DispatchAttribute
    // Index
}

// WGPU attribute:  WorkgroupSizeAttribute
public readonly record struct CsWorkgroupSize
{
    public required                 int     workgroupCountX { get; init; }
    public required                 int     workgroupCountY { get; init; }
    public required                 int     workgroupCountZ { get; init; }
    
    [Browse(Never)] public required SrcLoc  attrLoc { get; init; }
}

// WGPU attribute:  MapAttribute
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
    public required     string              Name                { get; init; }
    public required     CsWorkloadAttribute WorkloadAttribute   { get; init; }
    public required     CsParamAttribute    ParamAttribute      { get; init; }
    public required     CsType              Type                { get; init; }
    public required     CsBindGroup         BindGroup           { get; init; }
    public required     int                 VertexBufferSlot    { get; init; }
    public required     CsAttrEnum          AttrEnum            { get; init; }
    //
    [Browse(Never)] public required SrcLoc  TypeLoc             { get; init; }
    [Browse(Never)] public required SrcLoc  GenericArgLoc       { get; init; }
    [Browse(Never)] public required SrcLoc  NameLoc             { get; init; }
    [Browse(Never)] public required SrcLoc  AttrLoc             { get; init; }
    [Browse(Never)] public required SrcLoc  AttrArg0Loc         { get; init; }
    [Browse(Never)] public required SrcLoc  AttrArg1Loc         { get; init; }
    
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
    public required     CsTypeIdentifier    Identifier  { get; init; }
    public required     CsTypeLayout        TypeLayout  { get; init; }
    public required     ValueArray<CsField> Fields      { get; init; }
    public required     CsTypeCode          TypeCode    { get; init; }
    
    public override     string              ToString() => Identifier.Name;
}

public readonly record struct CsField
{
    public required     string              Name        { get; init; }
    public required     CsType              Type        { get; init; }

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
    public readonly     string              Name;
    public readonly     string              Namespace;

    public override     string              ToString()  => $"{Name}";
    
    public CsTypeIdentifier(string name) {
        Name        = name;
        Namespace   = "";
    }
    
    public CsTypeIdentifier(string name, string @namespace) {
        Name        = name;
        Namespace   = @namespace;
    }
}

public readonly record struct CsTypeLayout
{
    public readonly     int     Size;
    public readonly     int     Align;  // always >= 1
    
    public CsTypeLayout(int size, int align) {
        Size    = size;
        Align   = align;
        Debug.Assert(align > 0);
    }
}

public readonly record struct CsType
{
    public required     string              Name        { get; init; }
    public required     string              Namespace   { get; init; }
    public required     ValueArray<CsType>  Generics    { get; init; } // generic type arguments
    public required     bool                IsArray     { get; init; }
    public required     CsTypeCode          TypeCode    { get; init; }
    public required     CsTypeLayout        TypeLayout  { get; init; }

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

