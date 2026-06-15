// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


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
/// Handle to a unique <see cref="WgpuRenderPipelineDescriptor"/>
/// </summary>
public readonly struct RenderPipelineConfig
{
    public readonly     int                             Id;
    
    public              string                          Name        =>     WgpuRenderPipelineDescriptor.GetEntry(Id).name;
    public ref readonly WgpuRenderPipelineDescriptor    Descriptor  => ref WgpuRenderPipelineDescriptor.GetEntry(Id).descriptor;
    
    public override     string                          ToString()  => $"'{WgpuRenderPipelineDescriptor.GetEntry(Id).name}'";
    
    internal RenderPipelineConfig(int id) {
        Id = id;
    }
}

/// <summary> managed type for:  <see cref="RenderPipelineDescriptor"/> </summary>
/// <remarks>
/// After set up of a unique <see cref="WgpuRenderPipelineDescriptor"/> configuration
/// create a <see cref="RenderPipelineConfig"/> with <see cref="CreateConfig"/>.
/// </remarks>
public record struct WgpuRenderPipelineDescriptor
{
    public  WgpuPrimitiveState      PrimitiveState      = new();
    public  WgpuFragmentState?      FragmentState       = null;
    public  WgpuMultisampleState    MultisampleState    = new();
    public  WgpuVertexState         VertexState         = new();
    public  WgpuDepthStencilState?  DepthStencilState   = null;

    public WgpuRenderPipelineDescriptor()  { }
    
    /// <summary> Returns the default <see cref="RenderPipelineConfig"/> with a <see cref="FragmentState"/>. </summary>
    /// <remarks>
    /// To create a custom config create a new <see cref="WgpuRenderPipelineDescriptor"/><br/>
    /// instance and call <see cref="CreateConfig"/>.
    /// </remarks>
    public static RenderPipelineConfig DefaultRenderPipeline = new();
    
    /// <summary>
    /// Create a new <see cref="RenderPipelineConfig"/> or returns an existing<br/>
    /// if already one created with the same <see cref="WgpuRenderPipelineDescriptor"/> setup.
    /// </summary>
    /// <remarks>
    /// Example
    /// <code>
    ///     var desc = new WgpuRenderPipelineDescriptor();
    ///     desc.FragmentState = new WgpuFragmentState {
    ///         targets = [new WgpuColorTargetState { format = TextureFormat.BGRA8Unorm, writeMask = WebGPU_native.ColorWriteMask_All}]
    ///     };
    ///     var config = desc.CreateConfig("Custom Config");
    /// </code>
    /// </remarks>
    public RenderPipelineConfig CreateConfig(string name)
    {
        if (descriptorToId.TryGetValue(this, out var id)) {
            return new RenderPipelineConfig(id);
        }
        var descriptors = idToDescriptor;
        var config      = new RenderPipelineConfig(descriptors.Count);
        var entry       = new RenderPipelineEntry(name, this);
        descriptors.Add(entry);
        descriptorToId.Add(this, config.Id);
        return config;
    }
    
    internal static ref RenderPipelineEntry GetEntry(int id)
    {
        var span = CollectionsMarshal.AsSpan(idToDescriptor);
        return ref span[id];
    }
    
    private static readonly     List<RenderPipelineEntry>                       idToDescriptor;
    private static readonly     Dictionary<WgpuRenderPipelineDescriptor, int>   descriptorToId = [];
    
    static WgpuRenderPipelineDescriptor()
    {
        var defaultDesc = new WgpuRenderPipelineDescriptor {
            FragmentState = new WgpuFragmentState()
        };
        var entry       = new RenderPipelineEntry("Default Render Pipeline", defaultDesc);
        idToDescriptor  = [entry];
        descriptorToId.Add(entry.descriptor, 0);
    }
    
    internal readonly struct RenderPipelineEntry(string name, in WgpuRenderPipelineDescriptor descriptor)
    {
        internal readonly string                        name        = name;
        internal readonly WgpuRenderPipelineDescriptor  descriptor  = descriptor;
    }
}



// ---------------------------------------- top level wgpu states ----------------------------------------
/// <summary> managed type for:  <see cref="PrimitiveState"/> </summary>
public record struct WgpuPrimitiveState
{
    public  PrimitiveTopology   topology            = PrimitiveTopology.TriangleList;
    public  IndexFormat         stripIndexFormat;
    public  FrontFace           frontFace;
    public  CullMode            cullMode;
    public  uint                unclippedDepth;
    
    public WgpuPrimitiveState() { }
    
    internal readonly PrimitiveState GetNative() {
        return new PrimitiveState {
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
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuColorTargetState>    targets = [new() { format =  TextureFormat.BGRA8Unorm, writeMask = ColorWriteMask_All}];

    public WgpuFragmentState() { }

    internal readonly unsafe FragmentState GetNative(NativeAllocator allocator)
    {
        return new FragmentState {
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
    public  uint    count                   = 1;            // 1 = normal rendering (no MSAA), >1  for Anti-Aliasing
    public  uint    mask                    = 0xFFFFFFFF;   // (Standard)
    public  bool    alphaToCoverageEnabled;
    
    public WgpuMultisampleState() { }
    
    internal readonly MultisampleState GetNative() {
        return new MultisampleState {
            count                   = count,
            mask                    = mask,
            alphaToCoverageEnabled  = alphaToCoverageEnabled ? 1u : 0
        };
    }
}

/// <summary> managed type for:  <see cref="DepthStencilState"/> </summary>
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
    
    internal readonly DepthStencilState GetNative() {
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

/// <summary> managed type for:  <see cref="VertexState"/> </summary>
public record struct WgpuVertexState
{
//  public  ShaderModule*                       module;         defined via [Shader] attribute
//  public  StringView                          entryPoint;     defined via [Shader] attribute
    public  ValueArray<WgpuConstantEntry>       constants;
    public  ValueArray<WgpuVertexBufferLayout>  buffers;
    
    public WgpuVertexState() { }
    
    internal readonly unsafe VertexState GetNative(NativeAllocator allocator)
    {
        return new VertexState {
            constantCount   = (uint)constants.Length,
            constants       = allocator.ArrayToNative(constants, src => src.GetNative(allocator)),
            bufferCount     = (uint)buffers.Length,
            buffers         = allocator.ArrayToNative(buffers,   src => src.GetNative(allocator))
        };
    }
}



// ---------------------------------------- child level wgpu states ----------------------------------------

/// <summary> managed type for:  <see cref="ColorTargetState"/> </summary>
public record struct WgpuColorTargetState
{
    public  TextureFormat       format              = TextureFormat.BGRA8Unorm;
    public  BlendState?         blend;
    public  ulong               writeMask           = ColorWriteMask_All;
    
    public WgpuColorTargetState() { }
    
    internal readonly unsafe ColorTargetState GetNative(NativeAllocator allocator)
    {
        return new ColorTargetState {
            format      = format,
            writeMask   = writeMask,
            blend       = allocator.NullableToNative(blend, value => value)
        };
    }
}

/// <summary> managed type for:  <see cref="ConstantEntry"/> </summary>
public record struct WgpuConstantEntry
{
    public  string  key;
    public  double  value;
    
    internal readonly ConstantEntry GetNative(NativeAllocator allocator)
    {
        return new ConstantEntry {
            key     = allocator.StringToNative(key),
            value   = value
        };
    }
}

/// <summary> managed type for:  <see cref="VertexBufferLayout"/> </summary>
public record struct WgpuVertexBufferLayout
{
    public  VertexStepMode              stepMode;
    public  ulong                       arrayStride;
    public  ValueArray<VertexAttribute> attributes;
    
    internal readonly unsafe VertexBufferLayout GetNative(NativeAllocator allocator)
    {
        return new VertexBufferLayout {
            arrayStride     = arrayStride,
            stepMode        = stepMode,
            attributeCount  = (uint)attributes.Length,
            attributes      = allocator.ArrayToNative(attributes, src => src)
        };
    }
}
