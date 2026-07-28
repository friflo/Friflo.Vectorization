// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Friflo.WGSL.Transpiler.CSharp;

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

public record WgslType
{
    public  string                  Name        { get; set; } = string.Empty;
    public  ValueArray<WgslType>    Generics    { get; set; } // Geändert von ValueArray zu List für einfacheres Parsen, falls nötig im Code anpassen

    public override string ToString()
    {
        if (Generics.Length == 0) return Name;
        return $"{Name}<{string.Join(", ", Generics.Select(g => g.ToString()))}>";
    }
}

public class WgslStruct
{
    public string           Name        { get; set; } = string.Empty;
    public List<WgslField>  Fields      { get; set; } = [];
    public string           sourcePath;
    
    public override string ToString() => Name;
}

public class WgslField
{
    public  string      Name            { get; set; } = string.Empty;
    public  WgslType    WgslType        { get; set; } = new();
    
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
        return generics[index].Name;
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
