// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System.Collections.Generic;
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
    
    public RenderConfigDescriptor Descriptor => RenderConfigDescriptor.idToDescriptor[Id];
}

public record struct RenderConfigDescriptor
{
    public  WgpuColorTargetState    ColorTargetState    = new();
    public  WgpuPrimitiveState      PrimitiveState      = new();
    public  WgpuFragmentState       FragmentState       = new();
    public  WgpuMultisampleState    MultisampleState    = new();
    public  WgpuVertexState         VertexState         = new();
    public  WgpuDepthStencilState?  DepthStencilState   = null;
    
    public RenderConfigDescriptor() { }
    
    public RenderConfig GetConfig()
    {
        if (descriptorToId.TryGetValue(this, out var id)) {
            return new RenderConfig(id);
        }
        var descriptors = idToDescriptor;
        var config      = new RenderConfig(descriptors.Count);
        descriptors.Add(this);
        descriptorToId.Add(this, config.Id);
        return config;
    }
    
    internal static readonly    List<RenderConfigDescriptor>            idToDescriptor;
    internal static readonly    Dictionary<RenderConfigDescriptor, int> descriptorToId = [];
    
    static RenderConfigDescriptor()
    {
        var defaultDesc = new RenderConfigDescriptor();
        idToDescriptor = [defaultDesc];
        descriptorToId.Add(defaultDesc, 0);
    }
    
    internal unsafe DepthStencilState* NativeDepthStencilState(NativeAllocator allocator)
    {
        return allocator.NullableToNative(DepthStencilState, state => state.GetNative());
    }
}


// ---------------------------------------- top level wgpu states ----------------------------------------
/// <summary> managed type for <see cref="ColorTargetState"/> </summary>
public record struct WgpuColorTargetState
{
    public  TextureFormat       format              = TextureFormat.BGRA8Unorm;
    public  BlendState?         blend;
    public  ulong               writeMask           = ColorWriteMask_All;
    
    public WgpuColorTargetState() { }
}

/// <summary> managed type for <see cref="PrimitiveState"/> </summary>
public record struct WgpuPrimitiveState
{
    public  PrimitiveTopology   topology            = PrimitiveTopology.TriangleList;
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
            unclippedDepth      = unclippedDepth
        };
    }
}

/// <summary> managed type for <see cref="FragmentState"/> </summary>
public record struct WgpuFragmentState
{
//  public  string                              entryPoint;     defined via [Shader] attribute
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuColorTargetState>    targets = [new() { format =  TextureFormat.BGRA8Unorm, writeMask = ColorWriteMask_All}];

    public WgpuFragmentState() { }

    internal unsafe ColorTargetState* NativeTargets(NativeAllocator allocator)
    {
        return allocator.ArrayToNative(targets, src => new ColorTargetState {
            format      = src.format,
            writeMask   = src.writeMask,
            blend       = allocator.NullableToNative(src.blend, blend => blend)
        });
    }
    
    internal unsafe ConstantEntry* NativeConstants(NativeAllocator allocator)
    {
        return allocator.ArrayToNative(constants, src => src.GetNative(allocator));
    }
}

/// <summary> managed type for <see cref="MultisampleState"/> </summary>
public record struct WgpuMultisampleState
{
    public  uint    count                   = 1;            // 1 = normal rendering (no MSAA), >1  for Anti-Aliasing
    public  uint    mask                    = 0xFFFFFFFF;   // (Standard)
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

/// <summary> managed type for <see cref="DepthStencilState"/> </summary>
public record struct WgpuDepthStencilState
{
    public  TextureFormat       format;
    public  OptionalBool        depthWriteEnabled;
    public  CompareFunction     depthCompare;
    public  StencilFaceState    stencilFront;
    public  StencilFaceState    stencilBack;
    public  uint                stencilReadMask;
    public  uint                stencilWriteMask;
    public  int                 depthBias;
    public  float               depthBiasSlopeScale;
    public  float               depthBiasClamp;
    
    public WgpuDepthStencilState() { }
    
    internal DepthStencilState GetNative() {
        return new DepthStencilState {
            format              = format,
            depthWriteEnabled   = depthWriteEnabled,
            depthCompare        = depthCompare,
            stencilFront        = stencilFront,
            stencilBack         = stencilBack,
            stencilReadMask     = stencilReadMask,
            stencilWriteMask    = stencilWriteMask,
            depthBias           = depthBias,
            depthBiasSlopeScale = depthBiasSlopeScale,
            depthBiasClamp      = depthBiasClamp
        };
    }
}

/// <summary> managed type for <see cref="VertexState"/> </summary>
public record struct WgpuVertexState
{
//  public  ShaderModule*                       module;         defined via [Shader] attribute
//  public  StringView                          entryPoint;     defined via [Shader] attribute
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuVertexBufferLayout>  buffers;
    
    public WgpuVertexState() { }
    
    internal unsafe ConstantEntry* NativeConstants(NativeAllocator allocator)
    {
        return allocator.ArrayToNative(constants, src => src.GetNative(allocator));
    }
    
    internal unsafe VertexBufferLayout* NativeBuffers(NativeAllocator allocator)
    {
        return allocator.ArrayToNative(buffers, src => new VertexBufferLayout {
            arrayStride     = src.arrayStride,
            stepMode        = src.stepMode,
            attributeCount  = (uint)src.attributes.Length,
            attributes      = allocator.ArrayToNative(src.attributes, attribute
                => new VertexAttribute {
                    format          = attribute.format,
                    offset          = attribute.offset,
                    shaderLocation  = attribute.shaderLocation,
                }) 
        });
    }
}

// ---------------------------------------- child level wgpu states ----------------------------------------
/// <summary> managed type for <see cref="ConstantEntry"/> </summary>
public record struct WgpuConstantEntry
{
    public  string  key;
    public  double  value;
    
    internal ConstantEntry GetNative(NativeAllocator allocator)
    {
        return new ConstantEntry {
            key     = allocator.StringToNative(key),
            value   = value
        };
    }
}

/// <summary> managed type for <see cref="VertexBufferLayout"/> </summary>
public record struct WgpuVertexBufferLayout
{
    public  VertexStepMode              stepMode;
    public  ulong                       arrayStride;
    public  ValueArray<VertexAttribute> attributes;
    
    internal unsafe VertexAttribute* NativeAttributes(NativeAllocator allocator)
    {
        return allocator.ArrayToNative(attributes, src => new VertexAttribute {
            format          = src.format,
            offset          = src.offset,
            shaderLocation  = src.shaderLocation
        });
    }
}
