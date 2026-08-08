// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable once CheckNamespace
namespace Friflo.WGSL.Transpiler.WGSL;

// ==========================================
// AST / DATA MODELS
// ==========================================

public class WgslModule
{
    public List<WgslStruct>     Structs     { get; set; } = [];
    public List<WgslBinding>    Bindings    { get; set; } = [];
    public List<WgslEntryPoint> EntryPoints { get; set; } = [];
    public List<string>         Errors      { get; set; } = [];
    
    public void AddModule(WgslModule module)
    {
        Structs    .AddRange(module.Structs);
        Bindings   .AddRange(module.Bindings);
        EntryPoints.AddRange(module.EntryPoints);
        Errors     .AddRange(module.Errors);
    }
}

public struct WgslTypeGenerics
{
    public              WgslType?   Arg_0;  // type name like f32, vec3<f32>, ... or a struct name
    public              WgslType?   Arg_1;  // size of an array
    
    public              int         Length => Arg_1 != null ? 2 : Arg_0 != null ? 1 : 0;
}

public record WgslType
{
    public  required    string              Name     { get; set; }
    public              WgslTypeGenerics    Generics { get; set; }

    public  override    string              ToString()
    {
        switch (Generics.Length) {
            case 0:     return Name;
            case 1:     return $"{Name}<{Generics.Arg_0}>";
            default:    return $"{Name}<{Generics.Arg_0}, {Generics.Arg_1}>";
        }
    }
}

public class WgslStruct
{
    public  required    string      Name            { get; set; }
    public  required    WgslField[] Fields          { get; set; }
    
    public  override    string      ToString()  => Name;
}

public class WgslField
{
    public  required    string      Name            { get; set; }
    public  required    WgslType    WgslType        { get; set; }
    public  required    int?        Align           { get; set; }
    public  required    int?        Size            { get; set; }
    
    public  override    string      ToString()      => Name;
}

public record WgslBinding
{
    public  required    int         Group           { get; set; }
    public  required    int         Binding         { get; set; }
    public  required    string      Name            { get; set; }
    public  required    WgslType    WgslType        { get; set; }
    
    public  required    string      AddressSpace    { get; set; }   // e.g. "storage", "uniform", "private"
    public  required    string      AccessMode      { get; set; }   // e.g. "read", "write", "read_write"
    
    public  override    string      ToString()      => AsString();
    
    public string AsString()
    {
        if (AddressSpace == "") {
            return $"var {Name}: {WgslType}";
        }
        if (AccessMode == "") {
            return $"var<{AddressSpace}> {Name}: {WgslType}";
        }
        return $"var<{AddressSpace}, {AccessMode}> {Name}: {WgslType}";
    }
    
    public string? GetGenericNameAt(int index)
    {
        var generics = WgslType.Generics;
        if (index >= generics.Length) return string.Empty;
        switch (index) {
            case 0: return generics.Arg_0?.Name;
            case 1: return generics.Arg_1?.Name;
        }
        return string.Empty;
    }
}

public class WgslAttribute
{
    public  required    string          Name        { get; set; }
    public  required    string[]        Args        { get; set; }

    public  override    string          ToString() => $"@{Name}";
}

public class WgslEntryPoint
{
    public  required    string          Stage       { get; set; }
    public  required    WgslAttribute[] Attributes  { get; set; }
    public  required    string          Name        { get; set; }
    public  required    List<WgslParam> Parameters  { get; set; }
    public  required    WgslType        ReturnType  { get; set; }

    public  override    string          ToString() => $"{Name}  @{Stage}";
}

public class WgslParam
{
    public  required    string          Attribute   { get; set; }
    public  required    string          Name        { get; set; }
    public  required    WgslType        WgslType    { get; set; }
    
    public  override    string          ToString()  => Name;
}
