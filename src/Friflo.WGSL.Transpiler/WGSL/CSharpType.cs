// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.WGSL.Transpiler.WGSL;


public readonly struct CSharpType
{
    public readonly CSharpIdentifier    identifier;
    public readonly WgslTypeInfo        info;
    public readonly CSharpStruct        csharpStruct; // != null if struct
    
    public override string              ToString()  => info.ToString();

    internal CSharpType(string typeName, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = new CSharpIdentifier(typeName);
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
        
    internal CSharpType(CSharpIdentifier identifier, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = identifier;
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
}

public readonly struct CSharpIdentifier
{
    public readonly     string      Name;
    public readonly     string      Namespace;
    public readonly     bool        isUnmapped;

    public override     string              ToString()  => $"{Name}";
    
    public CSharpIdentifier(string name) {
        Name        = name;
        Namespace   = "";
    }
    
    public CSharpIdentifier(string name, string @namespace, bool isUnmapped) {
        Name            = name;
        Namespace       = @namespace;
        this.isUnmapped = isUnmapped;
    }
}

public struct CSharpField
{
    public required string          name;
    public required CSharpType      type;
    public          int             offset;
    public          int             size;

    public override string          ToString() => name;
}

public class CSharpStruct
{
    public required string          name;
    public required string          source;
    public required CSharpField[]   fields;
    public required TypeLayout      layout;
    
    public override string          ToString() => name;
}

internal struct LocalStruct
{
    public required CSharpStruct    csharpStruct;
    public required bool            alreadyDeclared;
    
    public override string          ToString() => csharpStruct.name ;
}


public readonly struct WgslType2CSharpType
{
    public readonly CsTypeCode          typeCode;
    public readonly CSharpIdentifier    identifier;

    public override string ToString() => $"{typeCode} - {identifier}";

    public WgslType2CSharpType(CsTypeCode typeCode, string @namespace, string name)
    {
        this.typeCode   = typeCode;
        identifier 		= new CSharpIdentifier(name, @namespace, false);
    }
}   

