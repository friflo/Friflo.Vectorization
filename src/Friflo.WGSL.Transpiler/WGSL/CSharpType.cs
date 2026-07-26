// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.WGSL.Transpiler.WGSL;


public readonly struct WgslType2CSharpType
{
    public readonly CsTypeCode          typeCode;
    public readonly CsTypeIdentifier    identifier;
    
    public WgslType2CSharpType(CsTypeCode typeCode, string name, string @namespace)
    {
        this.typeCode   = typeCode;
        identifier = new CsTypeIdentifier(name, @namespace);
    }
}

public readonly struct CSharpType
{
    public readonly CsTypeIdentifier    identifier;
    public readonly WgslTypeInfo        info;
    public readonly CSharpStruct        csharpStruct; // != null if struct
    
    public override string              ToString()  => info.ToString();

    internal CSharpType(string typeName, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = new CsTypeIdentifier(typeName);
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
        
    internal CSharpType(CsTypeIdentifier identifier, WgslTypeInfo info, CSharpStruct csharpStruct) {
        this.identifier     = identifier;
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
}