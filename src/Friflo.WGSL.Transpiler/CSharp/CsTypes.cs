// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedType.Global
// ReSharper disable ClassNeverInstantiated.Global
namespace Friflo.WGSL.Transpiler.CSharp;


public class CsMethod
{
    public required     CsTypeIdentifier        Identifier  { get; init; }
    public required     List<CsAttribute>       Attributes  { get; init; }
    public required     List<CsParameter>       Parameters  { get; init; }
}

public struct CsAttribute
{
    public required     CsTypeIdentifier        Identifier  { get; init; }
    public required     List<CsAttributeArg>    Args        { get; init; }
    
    public CsAttribute() { }
}

public struct CsAttributeArg
{
    public required     string                  Name        { get; init; }
    public required     string                  Value       { get; init; } // string or int
    
    public CsAttributeArg() { }
}

public struct CsParameter
{
    public required     List<CsAttribute>       Attributes  { get; init; }
    public required     CsType                  Type        { get; init; }
    public required     string                  Name        { get; init; }
    
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
}

public struct CsField
{
    public required     List<CsAttribute>       Attributes  { get; init; }
    public required     CsType                  Type        { get; init; }
    public required     string                  Name        { get; init; }
    
    public CsField() { }
}

public enum CsTypeKind
{
    Class,
    Struct
}

public struct CsTypeIdentifier
{
    public required     string              Name        { get; init; }
    public required     string              Namespace   { get; init; }
}


