// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

// ReSharper disable MergeIntoLogicalPattern
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace Friflo.WGSL.Transpiler.CSharp;

public readonly struct CsShaderSource
{
    public required     string  Shader          { get; init; }  // WGPU attribute:      ShaderAttribute
    public required     string  VertexShader    { get; init; }  // WGPU attribute:      VertexShaderAttribute
    public required     string  FragmentShader  { get; init; }  // WGPU attribute:      FragmentShaderAttribute
    public required     string  VertexEntry     { get; init; }  // attribute parameter: vert
    public required     string  FragmentEntry   { get; init; }  // attribute parameter: frag
}

// WGPU attribute:  DrawVertexIndexAttribute
public readonly struct CsDrawVertexIndex
{
    public required     uint    vertexCount     { get; init; }
    public required     uint    instanceCount   { get; init; }
    public required     uint    firstVertex     { get; init; }
    public required     uint    firstInstance   { get; init; }
}


public class CsMethod
{
    public required     string              Name            { get; init; }
    public required     CsShaderSource      Source          { get; init; }
    public required     CsDrawVertexIndex?  DrawVertexIndex { get; init; }
    public required     CsType              DeclaringType   { get; init; }
    public required     CsParameter[]       Parameters      { get; init; }
    
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

public readonly struct CsAttribute
{
    public required     CsTypeIdentifier        Identifier  { get; init; }
    public required     List<CsAttributeArg>    Args        { get; init; }
    
    public override     string                  ToString() => Identifier.ToString();
    
    public CsAttribute() { }
}

public readonly struct CsAttributeArg
{
    public required     string  Name        { get; init; }
    public required     string  Value       { get; init; } // string or int

    public override     string  ToString()  => $"{Value} ({Name})";

    public CsAttributeArg() { }
}

public readonly struct CsEnum {
    public required     string  Name   { get; init; }
    public required     ulong   Value  { get; init; }
    
    public override     string  ToString() => Name;
}

public readonly struct CsAttrEnum
{
    public required     CsEnum  enum1   { get; init; } // WGSL enum:  ST,    WGPU enum:  TextureFormat
    public required     CsEnum  enum2   { get; init; } // WGSL enum:  TSA
}

public enum CsParamAttribute
{
    None,
    // --- GpuBuffer<>
    VertexBuffer,   // WGPU attribute:  VertexBufferAttribute
    BindStorage,    // WGPU attribute:  BindStorageAttribute
    BindUniform,    // WGPU attribute:  BindUniformAttribute
    BindIndex,      // WGPU attribute:  BindIndexAttribute
    
    // --- GpuSampler
    SamplerFiltering,       // WGPU attribute:  SamplerFiltering
    SamplerNonFiltering,    // WGPU attribute:  SamplerNonFiltering
    SamplerComparison,      // WGPU attribute:  SamplerComparison
    
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

public enum CsDrawType
{
    None,
    Draw,               // WGPU attribute:  DrawAttribute
    DrawInstance,       // WGPU attribute:  DrawInstanceAttribute
    DrawFirstVertex,    // WGPU attribute:  DrawFirstVertexAttribute
    DrawFirstInstance,  // WGPU attribute:  DrawFirstInstanceAttribute
    // Index
}

public readonly struct CsBindGroup
{
    /// <summary>Also used for slot in [VertexBuffer(slot)] </summary>
    public required     int     group           { get; init; }
    public required     int     binding         { get; init; }
}

public readonly struct CsParameter
{
    public required     string              Name            { get; init; }
    public required     CsDrawType          DrawType        { get; init; }
    public required     CsParamAttribute    ParamAttribute  { get; init; }
    public required     CsType              Type            { get; init; }
    public required     CsBindGroup         BindGroup       { get; init; }
    public required     CsAttrEnum          AttrEnum        { get; init; }
    
    public override     string              ToString()      => AppendString(new StringBuilder()).ToString();
    
    public bool HasBindGroup        => ParamAttribute != CsParamAttribute.None &&
                                       ParamAttribute != CsParamAttribute.VertexBuffer;
    
    public bool IsReadOnlyBuffer    => Type.Identifier.Name == "InBuffer";
    
    public bool HasHandle           => !(ParamAttribute == CsParamAttribute.BindUniform && !IsBuffer);

    public bool IsBuffer {
        get {
            var typeName = Type.Identifier.Name;
            return typeName == "InBuffer" || typeName == "InOutBuffer";
        }
    }

    public StringBuilder AppendString(StringBuilder sb)
    {
        sb.Append(Name);
        sb.Append(" : ");
        Type.AppendString(sb);
        return sb;
    }
    public CsParameter() { }
}

/// Is a class - it has an identity
public class CsType
{
    public required     CsTypeIdentifier        Identifier  { get; init; }
    public required     List<CsTypeIdentifier>  Generics    { get; init; } // generic type arguments
    public required     CsAttribute[]           Attributes  { get; init; }
    public required     CsField[]               Fields      { get; set;  } // only set for struct's -> no cyclic dependencies
    
    public override     string                  ToString() => AppendString(new StringBuilder()).ToString();
    
    public StringBuilder AppendString(StringBuilder sb)
    {
        sb.Append($"{Identifier.Name}");
        if (Generics.Count == 0) return sb;
        sb.Append("<");
        foreach (var generic in Generics) {
            sb.Append(generic.Name);
            sb.Append(", ");
        }
        sb.Length -= 2;
        sb.Append(">");
        return sb;
    }
}

public readonly struct CsField
{
    public required     CsAttribute[]   Attributes  { get; init; }
    public required     CsType          Type        { get; init; }
    public required     string          Name        { get; init; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Name);
        sb.Append("        : ");
        Type.AppendString(sb);
        return sb.ToString();
    }

    public CsField() { }
}

public readonly struct CsTypeIdentifier
{
    public required     string  Name        { get; init; }
    public required     string  Namespace   { get; init; }

    public override     string  ToString() => $"{Namespace}.{Name}";
}

// --- modifier - not relevant for wgpu specific code
public readonly struct CsModifier
{
    public required     string	            MethodVisibility	{ get; init; }
    public required     bool                IsMethodStatic    	{ get; init; }
    public required     bool                IsClass             { get; init; }
    public required     CsParamModifier[]   ParamModifiers      { get; init; }
}

public readonly struct CsParamModifier
{
    public required     string	type	{ get; init; }
}

