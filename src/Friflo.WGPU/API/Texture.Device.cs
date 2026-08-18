// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.GPU;
using Friflo.WGPU.Runtime;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable UnassignedField.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable CheckNamespace
namespace Friflo.WGPU;


/// <summary>
/// Specifies 3D extents for GPU resources, screen back-buffers, and offscreen render targets.
/// </summary>
/// <remarks>
/// Maps directly to native WebGPU bindings. Supports C# 12 collection expressions (e.g., <c>[1920, 1080]</c>).
/// <para>
/// <b>Field Usage:</b>
/// <list type="bullet">
///   <item><term>Screen &amp; 2D Textures:</term><description><c>width</c> × <c>height</c>, <c>depthOrArrayLayers = 1</c></description></item>
///   <item><term>Texture Arrays / Cubemaps:</term><description>Face resolution + layer count (e.g., 6 for cubemaps)</description></item>
///   <item><term>3D Volume Textures:</term><description>Full voxel resolution (X × Y × Z)</description></item>
/// </list>
/// </para>
/// </remarks>
[CollectionBuilder(typeof(GpuExtent3DBuilder), nameof(GpuExtent3DBuilder.Create))]
public struct GpuExtent3D : IEnumerable<int>
{
    public  int     width;
    public  int     height              = 1;
    public  int     depthOrArrayLayers  = 1;
    
    public  float   AspectRatio => width / (float)height;
    
    public override string  ToString()  => depthOrArrayLayers <= 2 ? $"{width} x {height}" :  $"{width} x {height} x {depthOrArrayLayers}";
    
    public GpuExtent3D() { }
    
    public GpuExtent3D(int width, int height, int depthOrArrayLayers) {
        this.width              = width;
        this.height             = height;
        this.depthOrArrayLayers = depthOrArrayLayers;
    }
    
    public IEnumerator<int> GetEnumerator() => throw new NotImplementedException();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Internal compiler helper to enable the [...] collection expression for <see cref="GpuExtent3D"/>.
/// </summary>
public static class GpuExtent3DBuilder
{
    public static GpuExtent3D Create(ReadOnlySpan<int> items)
    {
        var size = new GpuExtent3D();
        if (items.Length > 0) size.width                = items[0];
        if (items.Length > 1) size.height               = items[1];
        if (items.Length > 2) size.depthOrArrayLayers   = items[2];
        return size;
    }
}


/// <summary> <see cref="TextureDescriptor"/> </summary>
public struct GpuTextureDescriptor
{
    public  nint                nextInChain;
    public  string              label;
    public  TextureUsage        usage;
    public  TextureDimension    dimension       = TextureDimension.D2D;
    public  GpuExtent3D         size;
    public  TextureFormat       format;
    public  int                 mipLevelCount   = 1;
    public  int                 sampleCount     = 1;
    public  TextureFormat[]     viewFormats;

    public GpuTextureDescriptor() { }
}

public static unsafe partial class WgpuExtensions
{
    /// <remarks>
    /// <remarks>Same behavior as: <a href="https://developer.mozilla.org/en-US/docs/Web/API/GPUDevice/createTexture">MDN: GPUDevice.createTexture()</a></remarks>
    /// </remarks>
    public static GpuTexture CreateTexture(this GpuDevice device, in GpuTextureDescriptor? descriptor = null)
    {
        var native  = new TextureDescriptor();
        var src     = descriptor ?? new GpuTextureDescriptor();
        native.nextInChain              = (ChainedStruct*)src.nextInChain;
        native.usage                    = (ulong)src.usage;
        native.dimension                = src.dimension;
        native.size.width               = (uint)src.size.width;
        native.size.height              = (uint)src.size.height;
        native.size.depthOrArrayLayers  = (uint)src.size.depthOrArrayLayers;
        native.format                   = src.format;
        native.mipLevelCount            = (uint)src.mipLevelCount;
        native.sampleCount              = (uint)src.sampleCount;
        native.viewFormatCount          = (uint)(src.viewFormats?.Length ?? 0);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(src.label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        native.label            = WgpuUtils.CopyToStringView(src.label, labelBuffer, labelMaxCount);
        
        var wgpuDevice = (WgpuDevice)device;
        fixed(TextureFormat* ptr = src.viewFormats) {
            native.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(wgpuDevice.DevicePtr, &native);
            return new GpuTexture(wgpuDevice, src, texture);
        }
    }
}