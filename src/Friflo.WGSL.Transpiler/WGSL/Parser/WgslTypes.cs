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
    public WgslType Arg_0;  // type name like f32, vec3<f32>, ... or a struct name
    public WgslType Arg_1;  // size of an array
    
    public int Length => Arg_1 != null ? 2 : Arg_0 != null ? 1 : 0;
}

public record WgslType
{
    public  string                  Name        { get; set; } = string.Empty;
    public  WgslTypeGenerics        Generics    { get; set; }

    public override string ToString()
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
    public string           Name        { get; set; } = string.Empty;
    public List<WgslField>  Fields      { get; set; } = [];
    
    public override string ToString() => Name;
}

public class WgslField
{
    public  string      Name            { get; set; } = string.Empty;
    public  WgslType    WgslType        { get; set; } = new();
    public  int?        Align           { get; set; }
    public  int?        Size            { get; set; }
    
    public override string ToString() => Name;
}

public record WgslBinding
{
    public  int         Group           { get; set; }
    public  int         Binding         { get; set; }
    public  string      Name            { get; set; } = string.Empty;
    public  WgslType    WgslType        { get; set; } = new();
    
    public  string      AddressSpace    { get; set; } = string.Empty; // e.g. "storage", "uniform", "private"
    public  string      AccessMode      { get; set; } = string.Empty;   // e.g. "read", "write", "read_write"
    
    public override string ToString() => AsString();
    
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
    
    public string GetGenericNameAt(int index)
    {
        if (WgslType == null) return string.Empty;
        var generics = WgslType.Generics;
        if (index >= generics.Length) return string.Empty;
        switch (index) {
            case 0: return generics.Arg_0.Name;
            case 1: return generics.Arg_1.Name;
        }
        return string.Empty;
    }
}

public class WgslEntryPoint
{
    public  string           Stage      { get; set; } = string.Empty;
    public  string           Name       { get; set; } = string.Empty;
    public  List<WgslParam>  Parameters { get; set; } = [];
    public  WgslType         ReturnType { get; set; } = new();

    public  override string ToString() => $"{Name}  @{Stage}";
}

public class WgslParam
{
    public  string          Attribute   { get; set; } = string.Empty;
    public  string          Name        { get; set; } = string.Empty;
    public  WgslType        WgslType    { get; set; } = new();
    
    public  override string ToString() => Name;
}
