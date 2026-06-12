// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Collections.Generic;

// ReSharper disable CollectionNeverQueried.Global
namespace Friflo.WGSL.Transpiler;

public class WgslShaderMetadata
{
    public List<WgslStruct>     Structs         { get; set; } = [];
    public List<WgslBinding>    Bindings        { get; set; } = [];
    public List<WgslEntryPoint> EntryPoints     { get; set; } = [];
}

public class WgslStruct
{
    public string           Name { get; set; } = string.Empty;
    public List<WgslField>  Fields { get; set; } = [];
}

public class WgslField
{
    public string   Name            { get; set; } = string.Empty;
    public string   WgslType        { get; set; } = string.Empty;
}

public class WgslBinding
{
    public int      Group           { get; set; }
    public int      Binding         { get; set; }
    public string   Name            { get; set; } = string.Empty;
    public string   WgslType        { get; set; } = string.Empty;
    public string   AccessMode      { get; set; } = string.Empty;
}

public class WgslEntryPoint
{
    public string           Stage       { get; set; } = string.Empty;
    public string           Name        { get; set; } = string.Empty;
    public List<WgslParam>  Parameters  { get; set; } = [];
    public string           ReturnType  { get; set; } = string.Empty;
}

public class WgslParam
{
    public string           Attribute   { get; set; } = string.Empty;
    public string           Name        { get; set; } = string.Empty;
    public string           WgslType    { get; set; } = string.Empty;
}
