// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Collections.Immutable;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ConvertToConstant.Global
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
    
    public RenderConfigDescriptor Descriptor => descriptors[Id];
    
    internal static readonly List<RenderConfigDescriptor> descriptors = [new()];
}

public struct RenderConfigDescriptor
{
    public  WgpuColorTargetState    ColorTargetState    = new();
    public  WgpuPrimitiveState      PrimitiveState      = new();
    public  WgpuFragmentState       FragmentState       = new();
    public  WgpuMultisampleState    MultisampleState    = new();
    public  WgpuVertexState         VertexState         = new();
    
    public RenderConfigDescriptor() { }
    
    public RenderConfig GetConfig()
    {
        var descriptors = RenderConfig.descriptors;
        var id          = descriptors.Count;
        descriptors.Add(this);
        return new RenderConfig(id);
    }
}


// --------------------------- top level states ---------------------------
public struct WgpuColorTargetState
{
    public  TextureFormat       format      = TextureFormat.BGRA8Unorm;
    public  BlendState?         blend;
    public  ulong               writeMask   = ColorWriteMask_All;
    
    public WgpuColorTargetState() { }
}

public struct WgpuPrimitiveState
{
    public  PrimitiveTopology   topology = PrimitiveTopology.TriangleList;
    public  IndexFormat         stripIndexFormat;
    public  FrontFace           frontFace;
    public  CullMode            cullMode;
    public  uint                unclippedDepth;
    
    public WgpuPrimitiveState() { }
    
    internal PrimitiveState GetNative() {
        return new PrimitiveState {
            topology            = topology,
            stripIndexFormat    = stripIndexFormat,
            frontFace           = frontFace,
            cullMode            = cullMode,
            unclippedDepth      =  unclippedDepth
        };
    }
}

public struct WgpuFragmentState
{
//  public  string                                  entryPoint;     defined via [Shader] attribute
    public  ImmutableArray<WgpuConstantEntry>       constants;
    public  ImmutableArray<WgpuColorTargetState>    targets = [new() { format =  TextureFormat.BGRA8Unorm, writeMask = ColorWriteMask_All}];

    public WgpuFragmentState() { }

    internal ColorTargetState[] GetTargets()
    {
        var array = new ColorTargetState[targets.Length];
        for (int n = 0; n < targets.Length; n++) {
            var src = targets[n];
            var dst = new ColorTargetState {
                format      =  src.format,
                writeMask   =  src.writeMask
            };
            array[n] = dst;            
        }
        return array;
    } 
}

public struct WgpuMultisampleState
{
    public  uint    count   = 1;            // 1 = normal rendering (no MSAA), >1  for Anti-Aliasing
    public  uint    mask    = 0xFFFFFFFF;   // (Standard)
    public  bool    alphaToCoverageEnabled;
    
    public WgpuMultisampleState() { }
    
    internal MultisampleState GetNative() {
        return new MultisampleState {
            count                   = count,
            mask                    = mask,
            alphaToCoverageEnabled  = alphaToCoverageEnabled ? 1u : 0
        };
    }
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

