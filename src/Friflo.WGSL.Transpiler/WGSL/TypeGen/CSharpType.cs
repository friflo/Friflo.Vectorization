// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.WGSL.Transpiler.CSharp;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;


public readonly struct CSharpType
{
    public readonly CSharpIdentifier    identifier;
    public readonly WgslTypeInfo        info;
    public readonly CSharpStruct?       csharpStruct; // != null if struct
    
    public override string              ToString()  => identifier.ToString();

    internal CSharpType(string typeName, TypeResolution resolution, WgslTypeInfo info, CSharpStruct? csharpStruct) {
        this.identifier     = new CSharpIdentifier(typeName, resolution);
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
        
    internal CSharpType(CSharpIdentifier identifier, WgslTypeInfo info, CSharpStruct? csharpStruct) {
        this.identifier     = identifier;
        this.info           = info;
        this.csharpStruct   = csharpStruct;
    }
}

public enum TypeResolution
{
    Resolved,   // WGSL type or struct with declaration in WGSL
    NotFound,   // A struct without declaration in WGSL
    Unmapped,   // WGSL type without mapping in wgsl-types.ini
    Created     // CSharpType created for a fixed size array
}


public readonly struct CSharpIdentifier
{
    public readonly     string          Name;
    public readonly     string          Namespace;
    public readonly     TypeResolution  resolution;

    public override     string          ToString()  => $"{Name}";
    
    public CSharpIdentifier(string name, TypeResolution resolution) {
        Name            = name;
        Namespace       = "";
        this.resolution = resolution;
    }
    
    public CSharpIdentifier(string name, string @namespace, TypeResolution resolution) {
        Name            = name;
        Namespace       = @namespace;
        this.resolution = resolution;
    }
}

public struct CSharpField
{
    public required string          name;
    public required CSharpType      type;
    public          int             offset;
    public          int             size;
    public          int?            wgslAlign;
    public          int?            wgslSize;

    public override string          ToString() => $"{name}  offset: {offset}";
}

public class CSharpStruct
{
    public required string          name;
    public required string?         source;
    public required CSharpField[]?  fields;
    public required TypeLayout      layout; // if created for a FixedSizeArray the element layout
    
    public override string          ToString() => $"{name}  -  size: {layout.size}  align: {layout.align}";
}

internal struct LocalStruct
{
    public required CSharpStruct    csharpStruct;
    public required bool            alreadyDeclared;
    
    public override string          ToString() => csharpStruct.name ;
}

internal struct FixedSizeArray
{
    public required string          Name;
    public required string          Namespace;
    public required string          source;
    
    public override string          ToString() => Name;
}

/// <summary>
/// Controls how array element strides are calculated for WGSL buffer layouts.
/// </summary>
public enum ArrayStride
{
    /// <summary>
    /// Natural stride (e.g. 4 bytes for f32). 
    /// Used for WGSL Storage Buffers (var&lt;storage&gt;) to keep data compact.
    /// </summary>
    Natural, // For storage buffers: var<storage, read> binding0 : StorageStruct;

    /// <summary>
    /// Padded stride (rounded up to multiples of 16 bytes). 
    /// Enforced by WGSL for Uniform Buffers (var&lt;uniform&gt;).
    /// </summary>
    PadTo16Bytes, // For uniform buffers: var<uniform> binding1 : UniformStruct;
}



