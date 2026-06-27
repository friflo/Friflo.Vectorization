// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
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
    public              int                             Revision    =>     WgpuRenderPipelineDescriptor.GetEntry(Id).revision;
    
    public override     string                          ToString()  => $"Id {Id} '{WgpuRenderPipelineDescriptor.GetEntry(Id).name}'";
    
    public void UpdateDescriptor(in WgpuRenderPipelineDescriptor descriptor)
    {
        ref var entry       = ref WgpuRenderPipelineDescriptor.GetEntry(Id);
        entry.descriptor    = descriptor;
        entry.revision++;
    }
    
    internal RenderConfig(int id) {
        Id = id;
    }
}

/// <summary> managed type for:  <see cref="RenderPipelineDescriptor"/> </summary>
/// <remarks>
/// After set up of a unique <see cref="WgpuRenderPipelineDescriptor"/> configuration
/// create a <see cref="RenderConfig"/> handle with <see cref="CreateConfig"/>.
/// </remarks>
public struct WgpuRenderPipelineDescriptor
{
    public  WgpuPrimitiveState                      PrimitiveState      = new();
    public  ValueNullable<WgpuFragmentState>        FragmentState       = null;
    public  WgpuMultisampleState                    MultisampleState    = new();
    public  WgpuVertexState                         VertexState         = new();
    public  ValueNullable<WgpuDepthStencilState>    DepthStencilState   = null;

    public WgpuRenderPipelineDescriptor()  { }
    
    /// <summary>
    /// Creates a new <see cref="RenderConfig"/> handle to an immutable <see cref="WgpuRenderPipelineDescriptor"/>.<br/>
    /// To change a <see cref="RenderConfig"/> use <see cref="RenderConfig.UpdateDescriptor"/>.
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
        var array   = descriptors;
        var id      = descriptorCount++;
        if (id >= array.Length) {
            array = WgpuUtils.Resize(ref descriptors, id + 1);
        }
        array[id] = new RenderPipelineEntry(name, this);
        return new RenderConfig(id);
    }
    
    internal static ref RenderPipelineEntry GetEntry(int id)
    {
        if (id == 0) {
            throw new NullReferenceException("using a default RenderConfig");
        }
        return ref descriptors[id];
    }
    
    private static  RenderPipelineEntry[]   descriptors     = [default];
    private static  int                     descriptorCount = 1;
    
    // ------ RenderPipelineEntry
    internal struct RenderPipelineEntry(string name, in WgpuRenderPipelineDescriptor descriptor)
    {
        internal readonly   string                          name        = name;
        internal            WgpuRenderPipelineDescriptor    descriptor  = descriptor;
        internal            int                             revision;
    }
}



// ---------------------------------------- top level wgpu states ----------------------------------------
/// <summary> managed type for:  <see cref="PrimitiveState"/> </summary>
public struct WgpuPrimitiveState
{
    public  nint                nextInChain;
    public  PrimitiveTopology   topology            = PrimitiveTopology.TriangleList;
    public  IndexFormat         stripIndexFormat;
    public  FrontFace           frontFace;
    public  CullMode            cullMode;
    public  uint                unclippedDepth;
    
    public WgpuPrimitiveState() { }
    
    internal readonly unsafe PrimitiveState GetNative() {
        return new PrimitiveState {
            nextInChain         = (ChainedStruct*)nextInChain,
            topology            = topology,
            stripIndexFormat    = stripIndexFormat,
            frontFace           = frontFace,
            cullMode            = cullMode,
            unclippedDepth      = unclippedDepth
        };
    }
}

/// <summary> managed type for:  <see cref="FragmentState"/> </summary>
public struct WgpuFragmentState
{
    public  string                              entryPoint;
    public  string                              module;
    public  nint                                nextInChain;
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuColorTargetState>    targets = [new() { format =  TextureFormat.BGRA8Unorm, writeMask = ColorWriteMask_All}];

    public WgpuFragmentState() { }

    internal readonly unsafe FragmentState GetNative(NativeAllocator allocator)
    {
        return new FragmentState {
            nextInChain     = (ChainedStruct*)nextInChain,
            targetCount     = (uint)targets.Length,
            targets         = allocator.ArrayToNative(targets,   src => src.GetNative(allocator)),
            constantCount   = (uint)constants.Length,
            constants       = allocator.ArrayToNative(constants, src => src.GetNative(allocator))
        };
    }
}

/// <summary> managed type for:  <see cref="MultisampleState"/> </summary>
public struct WgpuMultisampleState
{
    public  nint    nextInChain;
    public  uint    count                   = 1;            // 1 = normal rendering (no MSAA), >1  for Anti-Aliasing
    public  uint    mask                    = 0xFFFFFFFF;   // (Standard)
    public  bool    alphaToCoverageEnabled;
    
    public WgpuMultisampleState() { }
    
    internal readonly unsafe MultisampleState GetNative() {
        return new MultisampleState {
            nextInChain             = (ChainedStruct*)nextInChain,
            count                   = count,
            mask                    = mask,
            alphaToCoverageEnabled  = alphaToCoverageEnabled ? 1u : 0
        };
    }
}

/// <summary> managed type for:  <see cref="DepthStencilState"/> </summary>
public struct WgpuDepthStencilState
{
    public  nint                    nextInChain;
    public  TextureFormat           format;
    public  OptionalBool            depthWriteEnabled;
    public  CompareFunction         depthCompare;
    public  WgpuStencilFaceState    stencilFront;
    public  WgpuStencilFaceState    stencilBack;
    public  uint                    stencilReadMask;
    public  uint                    stencilWriteMask;
    public  int                     depthBias;
    public  float                   depthBiasSlopeScale;
    public  float                   depthBiasClamp;
    
    public WgpuDepthStencilState() { }
    
    internal readonly unsafe DepthStencilState GetNative() {
        return new DepthStencilState {
            nextInChain         = (ChainedStruct*)nextInChain,
            format              = format,
            depthWriteEnabled   = depthWriteEnabled,
            depthCompare        = depthCompare,
            stencilFront        = stencilFront.GetNative(),
            stencilBack         = stencilBack.GetNative(),
            stencilReadMask     = stencilReadMask,
            stencilWriteMask    = stencilWriteMask,
            depthBias           = depthBias,
            depthBiasSlopeScale = depthBiasSlopeScale,
            depthBiasClamp      = depthBiasClamp
        };
    }
}

/// <summary> managed type for:  <see cref="VertexState"/> </summary>
public struct WgpuVertexState
{
    public  nint                                nextInChain;
    public  string                              module;
    public  string                              entryPoint;
    public  ValueArray<WgpuConstantEntry>       constants;
    /// <summary>
    /// Note: VertexState buffer layouts should be global/standardized,
    /// so all compatible vertex buffers conform to the same structural layout contract.
    /// </summary>
    public  ValueArray<WgpuVertexBufferLayout>  buffers;
    
    public WgpuVertexState() { }
    
    internal readonly unsafe VertexState GetNative(NativeAllocator allocator)
    {
        return new VertexState {
            nextInChain     = (ChainedStruct*)nextInChain,
            constantCount   = (uint)constants.Length,
            constants       = allocator.ArrayToNative(constants, src => src.GetNative(allocator)),
            bufferCount     = (uint)buffers.Length,
            buffers         = allocator.ArrayToNative(buffers,   src => src.GetNative(allocator))
        };
    }
}



// ---------------------------------------- child level wgpu states ----------------------------------------


/// <summary> managed type for:  <see cref="ColorTargetState"/> </summary>
public struct WgpuColorTargetState
{
    public  nint                        nextInChain;
    public  TextureFormat               format              = TextureFormat.BGRA8Unorm;
    public  ValueNullable<BlendState>   blend;
    public  ulong                       writeMask           = ColorWriteMask_All;
    
    public WgpuColorTargetState() { }
    
    internal readonly unsafe ColorTargetState GetNative(NativeAllocator allocator)
    {
        return new ColorTargetState {
            nextInChain = (ChainedStruct*)nextInChain,
            format      = format,
            writeMask   = writeMask,
            blend       = allocator.NullableToNative(blend, value => value)
        };
    }
}

/// <summary> managed type for:  <see cref="ConstantEntry"/> </summary>
public struct WgpuConstantEntry
{
    public  nint    nextInChain;
    public  string  key;
    public  double  value;
    
    internal readonly unsafe ConstantEntry GetNative(NativeAllocator allocator)
    {
        return new ConstantEntry {
            nextInChain = (ChainedStruct*)nextInChain,
            key         = allocator.StringToNative(key),
            value       = value
        };
    }
}

/// <summary> managed type for:  <see cref="VertexBufferLayout"/> </summary>
public struct WgpuVertexBufferLayout
{
    public  nint                        nextInChain;
    public  VertexStepMode              stepMode;
    public  ulong                       arrayStride;
    public  ValueArray<VertexAttribute> attributes;
    
    internal readonly unsafe VertexBufferLayout GetNative(NativeAllocator allocator)
    {
        return new VertexBufferLayout {
            nextInChain     = (ChainedStruct*)nextInChain,
            arrayStride     = arrayStride,
            stepMode        = stepMode,
            attributeCount  = (uint)attributes.Length,
            attributes      = allocator.ArrayToNative(attributes, src => src)
        };
    }
}

/// <summary> managed type for:  <see cref="StencilFaceState"/> </summary>
// Added extra type to avoid using inefficient runtime-provided implementation
public struct WgpuStencilFaceState
{
    public  CompareFunction     compare;
    public  StencilOperation    failOp;
    public  StencilOperation    depthFailOp;
    public  StencilOperation    passOp;
    
    internal readonly StencilFaceState GetNative()
    {
        return new StencilFaceState {
            compare     = compare,
            failOp      = failOp,
            depthFailOp = depthFailOp,
            passOp      = passOp,
        };
    }
}
