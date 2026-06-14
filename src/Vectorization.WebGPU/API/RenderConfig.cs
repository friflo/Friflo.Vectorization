// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Immutable;
using Friflo.Vectorization.WebGPU.Runtime;

// ReSharper disable UnassignedField.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public readonly struct RenderConfig
{
    public readonly int Id;
    
    internal RenderConfig(int id) {
        Id = id;
    }
}

public struct RenderConfigDescriptor
{
    public  WgpuColorTargetState    ColorTargetState;
    public  WgpuPrimitiveState      PrimitiveState;
    public  WgpuFragmentState       FragmentState       =  new();
    public  WgpuMultisampleState    MultisampleState    =  new();
    public  WgpuVertexState         VertexState;
    
    public RenderConfigDescriptor() { }
    
    public RenderConfig GetConfig()
    {
        return new RenderConfig(0);
    }
}


// --------------------------- top level states ---------------------------
public struct WgpuColorTargetState
{
    public  TextureFormat       format;
    public  BlendState?         blend;
    public  ulong               writeMask;
}

public struct WgpuPrimitiveState
{
    public  PrimitiveTopology   topology;
    public  IndexFormat         stripIndexFormat;
    public  FrontFace           frontFace;
    public  CullMode            cullMode;
    public  uint                unclippedDepth;
}

public struct WgpuFragmentState
{
//  public  string                                  entryPoint;     defined via [Shader] attribute
    public  ImmutableArray<WgpuConstantEntry>       constants;
    public  ImmutableArray<WgpuColorTargetState>    targets;

    public WgpuFragmentState() { }
}

public struct WgpuMultisampleState
{
    public  uint    count;
    public  uint    mask;
    public  uint    alphaToCoverageEnabled;
}

public struct WgpuVertexState
{
//  public  ShaderModule*                           module;         defined via [Shader] attribute
//  public  StringView                              entryPoint;     defined via [Shader] attribute
    public  ImmutableArray<WgpuConstantEntry>       constants;
    public  ImmutableArray<WgpuVertexBufferLayout>  buffer;
    
    public WgpuVertexState() { }
}

// --------------------------- child level states ---------------------------
public struct WgpuConstantEntry
{
    public  string  key;
    public  double  value;
}

public struct WgpuVertexBufferLayout
{
    public  VertexStepMode                  stepMode;
    public  ulong                           arrayStride;
    public  ImmutableArray<VertexAttribute> attributes;
}



