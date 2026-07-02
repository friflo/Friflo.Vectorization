// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace Friflo.WGSL.Transpiler.CSharp;


public class CsMethod
{
    public required     string                  Name            { get; init; }
    public required     CsType                  DeclaringType   { get; init; }
    public required     List<CsAttribute>       Attributes      { get; init; }
    public required     List<CsParameter>       Parameters      { get; init; }
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"{Name}(");
        if (Parameters.Count == 0) {
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
    public required     string                  Name        { get; init; }
    public required     string                  Value       { get; init; } // string or int

    public override     string                  ToString()  => $"{Value} ({Name})";

    public CsAttributeArg() { }
}

public readonly struct CsParameter
{
    public required     List<CsAttribute>       Attributes  { get; init; }
    public required     CsType                  Type        { get; init; }
    public required     string                  Name        { get; init; }
    
    public override     string                  ToString() => AppendString(new StringBuilder()).ToString();
    
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
    public required     List<CsAttribute>       Attributes  { get; init; }
    public required     List<CsField>           Fields      { get; set; } // only set for CsTypeKind.Struct -> no cyclic dependencies
    
    public override     string                  ToString() => AppendString(new StringBuilder()).ToString();
    
    public StringBuilder AppendString(StringBuilder sb)
    {
        sb.Append($"{Identifier.Namespace}.{Identifier.Name}");
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
    public required     List<CsAttribute>       Attributes  { get; init; }
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

    public CsField() { }
}

public enum CsTypeKind
{
    Class,
    Struct
}

public readonly struct CsTypeIdentifier
{
    public required     string              Name        { get; init; }
    public required     string              Namespace   { get; init; }

    public override     string              ToString() => $"{Namespace}.{Name}";
}


