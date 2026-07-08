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
/// Handle to a unique <see cref="GpuRenderPipelineDescriptor"/>.<br/>
/// To create a <see cref="RenderConfig"/> see: <see cref="GpuRenderPipelineDescriptor.CreateConfig"/>.
/// </summary>
public readonly struct RenderConfig
{
    public readonly     int                         Id;
    
    public              string                      Name        =>     GpuRenderPipelineDescriptor.GetEntry(Id).name;
    public ref readonly GpuRenderPipelineDescriptor	Descriptor  => ref GpuRenderPipelineDescriptor.GetEntry(Id).descriptor;
    public              int                         Revision    =>     GpuRenderPipelineDescriptor.GetEntry(Id).revision;
    
    public override     string                      ToString()  => $"Id {Id} '{GpuRenderPipelineDescriptor.GetEntry(Id).name}'";
    
    public void UpdateDescriptor(in GpuRenderPipelineDescriptor descriptor)
    {
        ref var entry       = ref GpuRenderPipelineDescriptor.GetEntry(Id);
        entry.descriptor    = descriptor;
        entry.revision++;
    }
    
    internal RenderConfig(int id) {
        Id = id;
    }
}

/// <summary> managed type for:  <see cref="RenderPipelineDescriptor"/> </summary>
/// <remarks>
/// After set up of a unique <see cref="GpuRenderPipelineDescriptor"/> configuration
/// create a <see cref="RenderConfig"/> handle with <see cref="CreateConfig"/>.
/// </remarks>
public struct GpuRenderPipelineDescriptor
{
    public  GpuPrimitiveState                   PrimitiveState      = new();
    public  ValueNullable<GpuFragmentState>     FragmentState       = null;
    public  GpuMultisampleState                 MultisampleState    = new();
    public  GpuVertexState                      VertexState         = new();
    public  ValueNullable<GpuDepthStencilState>	DepthStencilState   = null;

    public GpuRenderPipelineDescriptor()  { }
    
    /// <summary>
    /// Creates a new <see cref="RenderConfig"/> handle to an immutable <see cref="GpuRenderPipelineDescriptor"/>.<br/>
    /// To change a <see cref="RenderConfig"/> use <see cref="RenderConfig.UpdateDescriptor"/>.
    /// </summary>
    /// <remarks>
    /// Example
    /// <code>
    ///     var fragmentState   = Surface.GetPreferredFragmentState(Adapter, true, out AlphaMode);
    ///     SwapChainFormat     = fragmentState.targets[0].format;
    ///     var desc            = new GpuRenderPipelineDescriptor { FragmentState = fragmentState };
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
    internal struct RenderPipelineEntry(string name, in GpuRenderPipelineDescriptor descriptor)
    {
        internal readonly   string                      name        = name;
        internal            GpuRenderPipelineDescriptor	descriptor  = descriptor;
        internal            int                         revision;
    }
}



// ---------------------------------------- top level wgpu states ----------------------------------------
/// <summary> managed type for:  <see cref="PrimitiveState"/> </summary>
public struct GpuPrimitiveState
{
    public  nint                nextInChain;
    public  PrimitiveTopology	topology            = PrimitiveTopology.TriangleList;
    public  IndexFormat         stripIndexFormat;
    public  FrontFace           frontFace;
    public  CullMode            cullMode;
    public  int                 unclippedDepth;
    
    public GpuPrimitiveState() { }
    
    internal readonly unsafe PrimitiveState GetNative() {
        return new PrimitiveState {
            nextInChain         = (ChainedStruct*)nextInChain,
            topology            = topology,
            stripIndexFormat    = stripIndexFormat,
            frontFace           = frontFace,
            cullMode            = cullMode,
            unclippedDepth      = (uint)unclippedDepth
        };
    }
}

/// <summary> managed type for:  <see cref="FragmentState"/> </summary>
public struct GpuFragmentState
{
    public  string                          entryPoint;
    public  string                          module;
    public  nint                            nextInChain;
    public  ValueArray<GpuConstantEntry>    constants;
    public  ValueArray<GpuColorTargetState>	targets = [new() { format =  TextureFormat.BGRA8Unorm, writeMask = ColorWriteMask_All}];

    public GpuFragmentState() { }

    internal readonly unsafe FragmentState GetNative(NativeAllocator allocator)
    {
        return new FragmentState {
            nextInChain     = (ChainedStruct*)nextInChain,
            targetCount     = (uint)targets.Length,
            targets         = allocator.ArrayToNative<GpuColorTargetState, ColorTargetState>(targets),
            constantCount   = (uint)constants.Length,
            constants       = allocator.ArrayToNative<GpuConstantEntry, ConstantEntry>(constants)
        };
    }
}

/// <summary> managed type for:  <see cref="MultisampleState"/> </summary>
public struct GpuMultisampleState
{
    public  nint    nextInChain;
    public  int     count                   = 1;            // 1 = normal rendering (no MSAA), >1  for Anti-Aliasing
    public  uint    mask                    = 0xFFFFFFFF;   // (Standard)
    public  bool    alphaToCoverageEnabled;
    
    public GpuMultisampleState() { }
    
    internal readonly unsafe MultisampleState GetNative() {
        return new MultisampleState {
            nextInChain             = (ChainedStruct*)nextInChain,
            count                   = (uint)count,
            mask                    = mask,
            alphaToCoverageEnabled  = alphaToCoverageEnabled ? 1u : 0
        };
    }
}

/// <summary> managed type for:  <see cref="DepthStencilState"/> </summary>
public struct GpuDepthStencilState
{
    public  nint                nextInChain;
    public  TextureFormat       format;
    public  bool?               depthWriteEnabled;
    public  CompareFunction     depthCompare;
    public  GpuStencilFaceState stencilFront;
    public  GpuStencilFaceState	stencilBack;
    public  uint                stencilReadMask;
    public  uint                stencilWriteMask;
    public  int                 depthBias;
    public  float               depthBiasSlopeScale;
    public  float               depthBiasClamp;
    
    internal readonly unsafe DepthStencilState GetNative() {
        var depthWrite = depthWriteEnabled.HasValue ? (depthWriteEnabled.Value ? OptionalBool.True : OptionalBool.False) : OptionalBool.Undefined;
        return new DepthStencilState {
            nextInChain         = (ChainedStruct*)nextInChain,
            format              = format,
            depthWriteEnabled   = depthWrite,
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
public struct GpuVertexState
{
    public  nint                                nextInChain;
    public  string                              module;
    public  string                              entryPoint;
    public  ValueArray<GpuConstantEntry>		constants;
    /// <summary>
    /// Note: VertexState buffer layouts should be global/standardized,
    /// so all compatible vertex buffers conform to the same structural layout contract.
    /// </summary>
    public  ValueArray<GpuVertexBufferLayout>  	buffers;
    
    internal readonly unsafe VertexState GetNative(NativeAllocator allocator)
    {
        return new VertexState {
            nextInChain     = (ChainedStruct*)nextInChain,
            constantCount   = (uint)constants.Length,
            constants       = allocator.ArrayToNative<GpuConstantEntry, ConstantEntry>(constants),
            bufferCount     = (uint)buffers.Length,
            buffers         = allocator.ArrayToNative<GpuVertexBufferLayout, VertexBufferLayout>(buffers)
        };
    }
}



// ---------------------------------------- child level wgpu states ----------------------------------------


/// <summary> managed type for:  <see cref="ColorTargetState"/> </summary>
public struct GpuColorTargetState : INativeSource<ColorTargetState>
{
    public  nint                            nextInChain;
    public  TextureFormat                   format              = TextureFormat.BGRA8Unorm;
    public  ValueNullable<GpuBlendState>	blend;
    public  ulong                           writeMask           = ColorWriteMask_All;
    
    public GpuColorTargetState() { }
    
    readonly unsafe ColorTargetState INativeSource<ColorTargetState>.GetNative(NativeAllocator allocator)
    {
        return new ColorTargetState {
            nextInChain = (ChainedStruct*)nextInChain,
            format      = format,
            writeMask   = writeMask,
            blend       = allocator.NullableToNative(blend, static value => new BlendState {
                color = new BlendComponent {
                    operation = value.color.operation,
                    srcFactor = value.color.srcFactor,
                    dstFactor = value.color.dstFactor
                },
                alpha = new BlendComponent {
                    operation = value.alpha.operation,
                    srcFactor = value.alpha.srcFactor,
                    dstFactor = value.alpha.dstFactor
                }
            })
        };
    }
}

/// <summary> managed type for:  <see cref="ConstantEntry"/> </summary>
public struct GpuConstantEntry : INativeSource<ConstantEntry>
{
    public  nint    nextInChain;
    public  string  key;
    public  double  value;
    
    readonly unsafe ConstantEntry INativeSource<ConstantEntry>.GetNative(NativeAllocator allocator)
    {
        return new ConstantEntry {
            nextInChain = (ChainedStruct*)nextInChain,
            key         = allocator.StringToNative(key),
            value       = value
        };
    }
}

/// <summary> managed type for:  <see cref="VertexBufferLayout"/> </summary>
public struct GpuVertexBufferLayout : INativeSource<VertexBufferLayout>
{
    public  nint                            nextInChain;
    public  VertexStepMode                  stepMode;
    public  int                             arrayStride;
    public  ValueArray<GpuVertexAttribute> 	attributes;
    
    readonly unsafe VertexBufferLayout INativeSource<VertexBufferLayout>.GetNative(NativeAllocator allocator)
    {
        return new VertexBufferLayout {
            nextInChain     = (ChainedStruct*)nextInChain,
            arrayStride     = (ulong)arrayStride,
            stepMode        = stepMode,
            attributeCount  = (uint)attributes.Length,
            attributes      = allocator.ArrayToNative<GpuVertexAttribute, VertexAttribute>(attributes)
        };
    }
}

/// <summary> managed type for:  <see cref="VertexAttribute"/> </summary>
public struct GpuVertexAttribute : INativeSource<VertexAttribute>
{
    public  nint            nextInChain;
    public  VertexFormat    format;
    public  int             offset;
    public  int             shaderLocation;
  
    readonly unsafe VertexAttribute INativeSource<VertexAttribute>.GetNative(NativeAllocator allocator)
    {
        return new VertexAttribute {
            nextInChain     = (ChainedStruct*)nextInChain,
            format          = format,
            offset          = (ulong)offset,
            shaderLocation  = (uint)shaderLocation,
        };
    }
}

/// <summary> managed type for:  <see cref="StencilFaceState"/> </summary>
public struct GpuStencilFaceState
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

/// <summary> managed type for:  <see cref="BlendState"/> </summary>
public struct GpuBlendState
{
  public    GpuBlendComponent  	color;
  public    GpuBlendComponent  	alpha;
}

/// <summary> managed type for:  <see cref="BlendComponent"/> </summary>
public struct GpuBlendComponent
{
  public    BlendOperation	operation;
  public    BlendFactor     srcFactor;
  public    BlendFactor     dstFactor;
}
