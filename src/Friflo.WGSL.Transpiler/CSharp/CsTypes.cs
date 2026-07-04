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
    public required     string          Shader          { get; init; }
    public required     string          VertexShader    { get; init; }
    public required     string          FragmentShader  { get; init; }
    public required     string          VertexEntry     { get; init; }
    public required     string          FragmentEntry   { get; init; }
}


public class CsMethod
{
    public required     string          Name            { get; init; }
    public required     CsShaderSource  Source          { get; init; }
    public required     CsType          DeclaringType   { get; init; }
    public required     CsParameter[]   Parameters      { get; init; }
    
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

public enum CsSampleType
{
    None,
    i32,
    u32,
    f32
}

public enum CsParamAttribute
{
    None,
    //
    VertexBuffer,
    //
    BindStorage,
    BindUniform,
    BindIndex,
    //
    SamplerFiltering,
    SamplerNonFiltering,
    SamplerComparison,
    //
    texture_1d,
    texture_2d,
    texture_2d_array,
    texture_3d,
    texture_cube,
    texture_cube_array,
    texture_multisampled_2d,
    texture_depth_multisampled_2d,
    texture_storage_1d,
    texture_storage_2d,
    texture_storage_2d_array,
    texture_storage_3d,
    texture_depth_2d,
    texture_depth_2d_array,
    texture_depth_cube,
    texture_depth_cube_array
}

public readonly struct CsParameter
{
    public required     CsParamAttribute    ParamAttribute  { get; init; }
    public required     CsType              Type            { get; init; }
    public required     string              Name            { get; init; }
    public required     int                 GroupIndex      { get; init; }
    public required     int                 BindingIndex    { get; init; }
    public required     CsSampleType        SampleType      { get; init; }
    
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
    public required     CsTypeKind              Kind        { get; init; }
    public required     List<CsTypeIdentifier>  Generics    { get; init; }  // generic type arguments
    public required     CsAttribute[]           Attributes  { get; init; }
    public required     CsField[]               Fields      { get; set; } // only set for CsTypeKind.Struct -> no cyclic dependencies
    
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

public enum CsTypeKind
{
    Class,
    Struct
}

public readonly struct CsTypeIdentifier
{
    public required     string  Name        { get; init; }
    public required     string  Namespace   { get; init; }

    public override     string  ToString() => $"{Namespace}.{Name}";
}


