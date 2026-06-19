// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

/// <summary>
/// Handle to a unique <see cref="WgpuRenderPipelineDescriptor"/>.<br/>
/// To create a <see cref="RenderConfig"/> see: <see cref="WgpuRenderPipelineDescriptor.CreateConfig"/>.
/// </summary>
public readonly struct RenderConfig
{
    public readonly     int                             Id;
    
    public              string                          Name        =>     WgpuRenderPipelineDescriptor.GetEntry(Id).name;
    public ref readonly WgpuRenderPipelineDescriptor    Descriptor  => ref WgpuRenderPipelineDescriptor.GetEntry(Id).descriptor;
    
    public override     string                          ToString()  => $"'{WgpuRenderPipelineDescriptor.GetEntry(Id).name}'";
    
    internal RenderConfig(int id) {
        Id = id;
    }
}

/// <summary> managed type for:  <see cref="RenderPipelineDescriptor"/> </summary>
/// <remarks>
/// After set up of a unique <see cref="WgpuRenderPipelineDescriptor"/> configuration
/// create a <see cref="RenderConfig"/> handle with <see cref="CreateConfig"/>.
/// </remarks>
public record struct WgpuRenderPipelineDescriptor
{
    public  WgpuPrimitiveState      PrimitiveState      = new();
    public  WgpuFragmentState?      FragmentState       = null;
    public  WgpuMultisampleState    MultisampleState    = new();
    public  WgpuVertexState         VertexState         = new();
    public  WgpuDepthStencilState?  DepthStencilState   = null;

    public WgpuRenderPipelineDescriptor()  { }
    
    /// <summary>
    /// Create a new <see cref="RenderConfig"/> handle or returns an existing<br/>
    /// if already one created with the same <see cref="WgpuRenderPipelineDescriptor"/> setup.
    /// </summary>
    /// <remarks>
    /// Example
    /// <code>
    ///     var fragmentState   = Surface.GetPreferredFragmentState(Adapter, true, out AlphaMode);
    ///     SwapChainFormat     = fragmentState.targets[0].format;
    ///     var desc            = new WgpuRenderPipelineDescriptor { FragmentState = fragmentState };
    ///     Config              = desc.CreateConfig("Wgpu.Config");
    /// </code>
    /// </remarks>
    public RenderConfig CreateConfig(string name)
    {
        var list    = descriptorList;
        int testId  = list.Count;
        var entry   = new RenderPipelineEntry(name, this);
        list.Add(entry);
        
        if (descriptorToId.TryGetValue(testId, out var id)) {
            list.RemoveAt(testId);
            return new RenderConfig(id);
        }
        descriptorToId.Add(testId);
        return new RenderConfig(testId);
    }
    
    internal static ref RenderPipelineEntry GetEntry(int id)
    {
        if (id == 0) {
            throw new NullReferenceException("when using a default RenderConfig");
        }
        var span = CollectionsMarshal.AsSpan(descriptorList);
        return ref span[id];
    }
    
    private static readonly     List<RenderPipelineEntry>   descriptorList = [default];
    private static readonly     HashSet<int>                descriptorToId = new (new DescriptorIdComparer());
    
    // ------ RenderPipelineEntry
    internal readonly struct RenderPipelineEntry(string name, in WgpuRenderPipelineDescriptor descriptor)
    {
        internal readonly string                        name        = name;
        internal readonly WgpuRenderPipelineDescriptor  descriptor  = descriptor;
    }
    
    // ------ DescriptorIdComparer
    internal readonly struct DescriptorIdComparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y)
        {
            return GetEntry(x).descriptor.Equals(GetEntry(y).descriptor);
        }

        public int GetHashCode(int id)
        {
            return GetEntry(id).descriptor.GetHashCode();
        }
    }
}



// ---------------------------------------- top level wgpu states ----------------------------------------
/// <summary> managed type for:  <see cref="PrimitiveState"/> </summary>
public record struct WgpuPrimitiveState
{
    public  WgpuChainedStruct   nextInChain;
    public  PrimitiveTopology   topology            = PrimitiveTopology.TriangleList;
    public  IndexFormat         stripIndexFormat;
    public  FrontFace           frontFace;
    public  CullMode            cullMode;
    public  uint                unclippedDepth;
    
    public WgpuPrimitiveState() { }
    
    internal readonly unsafe PrimitiveState GetNative() {
        return new PrimitiveState {
            nextInChain         = nextInChain.GetValue(),
            topology            = topology,
            stripIndexFormat    = stripIndexFormat,
            frontFace           = frontFace,
            cullMode            = cullMode,
            unclippedDepth      = unclippedDepth
        };
    }
}

/// <summary> managed type for:  <see cref="FragmentState"/> </summary>
public record struct WgpuFragmentState
{
//  public  string                              entryPoint;     defined via [Shader] attribute
    public  WgpuChainedStruct                   nextInChain;
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuColorTargetState>    targets = [new() { format =  TextureFormat.BGRA8Unorm, writeMask = ColorWriteMask_All}];

    public WgpuFragmentState() { }

    internal readonly unsafe FragmentState GetNative(NativeAllocator allocator)
    {
        return new FragmentState {
            nextInChain     = nextInChain.GetValue(),
            targetCount     = (uint)targets.Length,
            targets         = allocator.ArrayToNative(targets,   src => src.GetNative(allocator)),
            constantCount   = (uint)constants.Length,
            constants       = allocator.ArrayToNative(constants, src => src.GetNative(allocator))
        };
    }
}

/// <summary> managed type for:  <see cref="MultisampleState"/> </summary>
public record struct WgpuMultisampleState
{
    public  WgpuChainedStruct   nextInChain;
    public  uint                count                   = 1;            // 1 = normal rendering (no MSAA), >1  for Anti-Aliasing
    public  uint                mask                    = 0xFFFFFFFF;   // (Standard)
    public  bool                alphaToCoverageEnabled;
    
    public WgpuMultisampleState() { }
    
    internal readonly unsafe MultisampleState GetNative() {
        return new MultisampleState {
            nextInChain             = nextInChain.GetValue(),
            count                   = count,
            mask                    = mask,
            alphaToCoverageEnabled  = alphaToCoverageEnabled ? 1u : 0
        };
    }
}

/// <summary> managed type for:  <see cref="DepthStencilState"/> </summary>
public record struct WgpuDepthStencilState
{
    public  WgpuChainedStruct   nextInChain;
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
    
    internal readonly unsafe DepthStencilState GetNative() {
        return new DepthStencilState {
            nextInChain         = nextInChain.GetValue(),
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

/// <summary> managed type for:  <see cref="VertexState"/> </summary>
public record struct WgpuVertexState
{
    public  WgpuChainedStruct                   nextInChain;
//  public  ShaderModule*                       module;         defined via [Shader] attribute
//  public  StringView                          entryPoint;     defined via [Shader] attribute
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuVertexBufferLayout>  buffers;
    
    public WgpuVertexState() { }
    
    internal readonly unsafe VertexState GetNative(NativeAllocator allocator)
    {
        return new VertexState {
            nextInChain     = nextInChain.GetValue(),
            constantCount   = (uint)constants.Length,
            constants       = allocator.ArrayToNative(constants, src => src.GetNative(allocator)),
            bufferCount     = (uint)buffers.Length,
            buffers         = allocator.ArrayToNative(buffers,   src => src.GetNative(allocator))
        };
    }
}



// ---------------------------------------- child level wgpu states ----------------------------------------

/// <summary> managed type for:  <see cref="ChainedStruct"/> </summary>
public unsafe struct WgpuChainedStruct : IEquatable<WgpuChainedStruct>
{
    public ChainedStruct* value;
    
    public readonly ChainedStruct* GetValue() => value;
    
    public bool Equals(WgpuChainedStruct other) {
        return true;
    }

    public override int GetHashCode() => 0;
}

/// <summary> managed type for:  <see cref="ColorTargetState"/> </summary>
public record struct WgpuColorTargetState
{
    public  WgpuChainedStruct   nextInChain;
    public  TextureFormat       format              = TextureFormat.BGRA8Unorm;
    public  BlendState?         blend;
    public  ulong               writeMask           = ColorWriteMask_All;
    
    public WgpuColorTargetState() { }
    
    internal readonly unsafe ColorTargetState GetNative(NativeAllocator allocator)
    {
        return new ColorTargetState {
            nextInChain = nextInChain.GetValue(),
            format      = format,
            writeMask   = writeMask,
            blend       = allocator.NullableToNative(blend, value => value)
        };
    }
}

/// <summary> managed type for:  <see cref="ConstantEntry"/> </summary>
public record struct WgpuConstantEntry
{
    public  WgpuChainedStruct   nextInChain;
    public  string              key;
    public  double              value;
    
    internal readonly unsafe ConstantEntry GetNative(NativeAllocator allocator)
    {
        return new ConstantEntry {
            nextInChain = nextInChain.GetValue(),
            key         = allocator.StringToNative(key),
            value       = value
        };
    }
}

/// <summary> managed type for:  <see cref="VertexBufferLayout"/> </summary>
public record struct WgpuVertexBufferLayout
{
    public  WgpuChainedStruct           nextInChain;
    public  VertexStepMode              stepMode;
    public  ulong                       arrayStride;
    public  ValueArray<VertexAttribute> attributes;
    
    internal readonly unsafe VertexBufferLayout GetNative(NativeAllocator allocator)
    {
        return new VertexBufferLayout {
            nextInChain     = nextInChain.GetValue(),
            arrayStride     = arrayStride,
            stepMode        = stepMode,
            attributeCount  = (uint)attributes.Length,
            attributes      = allocator.ArrayToNative(attributes, src => src)
        };
    }
}
