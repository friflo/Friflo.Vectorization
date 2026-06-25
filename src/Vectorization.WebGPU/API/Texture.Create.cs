// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


public sealed unsafe partial class WgpuDevice
{
#region ========================= General & Multiline Texture Types

    public GpuTexture1D CreateTexture1D(int width, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D1D, width, 1, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTexture1D(this, desc, texture, label);
        }
    }

    public GpuTexture2D CreateTexture2D(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTexture2D(this, desc, texture, label);
        }
    }

    public GpuTexture2DArray CreateTexture2DArray(int width, int height, int arrayLayerCount, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = (uint)arrayLayerCount;
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTexture2DArray(this, desc, texture, label);
        }
    }

    public GpuTexture3D CreateTexture3D(int width, int height, int depth, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D3D, width, height, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = (uint)depth;
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTexture3D(this, desc, texture, label);
        }
    }

    public GpuTextureCube CreateTextureCube(int size, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, size, size, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = 6;
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureCube(this, desc, texture, label);
        }
    }

    public GpuTextureCubeArray CreateTextureCubeArray(int size, int cubeCount, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, size, size, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = (uint)(cubeCount * 6);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureCubeArray(this, desc, texture, label);
        }
    }

#endregion

#region =========================  Depth Texture Types

    public GpuTextureDepth2D CreateTextureDepth2D(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureDepth2D(this, desc, texture, label);
        }
    }

    public GpuTextureDepth2DArray CreateTextureDepth2DArray(int width, int height, int arrayLayerCount, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = (uint)arrayLayerCount;
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureDepth2DArray(this, desc, texture, label);
        }
    }

    public GpuTextureDepthCube CreateTextureDepthCube(int size, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, size, size, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = 6;
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureDepthCube(this, desc, texture, label);
        }
    }

    public GpuTextureDepthCubeArray CreateTextureDepthCubeArray(int size, int cubeCount, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, size, size, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = (uint)(cubeCount * 6);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureDepthCubeArray(this, desc, texture, label);
        }
    }

#endregion

#region =========================  Multisampled Texture Types

    public GpuTextureMultisampled2D CreateTextureMultisampled2D(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureMultisampled2D(this, desc, texture, label);
        }
    }

    public GpuTextureDepthMultisampled2D CreateTextureDepthMultisampled2D(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureDepthMultisampled2D(this, desc, texture, label);
        }
    }

#endregion

#region =========================  Storage Texture Types

    public GpuTextureStorage1D CreateTextureStorage1D(int width, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D1D, width, 1, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureStorage1D(this, desc, texture, label);
        }
    }

    public GpuTextureStorage2D CreateTextureStorage2D(int width, int height, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureStorage2D(this, desc, texture, label);
        }
    }

    public GpuTextureStorage2DArray CreateTextureStorage2DArray(int width, int height, int arrayLayerCount, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D2D, width, height, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = (uint)arrayLayerCount;
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureStorage2DArray(this, desc, texture, label);
        }
    }

    public GpuTextureStorage3D CreateTextureStorage3D(int width, int height, int depth, TextureFormat format, TextureUsage usage, string label = null, in TextureOptions? options = null, Span<TextureFormat> viewFormats = default)
    {
        var desc = new TextureDescriptor();
        SetTextureDescriptor(ref desc, TextureDimension.D3D, width, height, format, usage, in options, viewFormats.Length);
        desc.size.depthOrArrayLayers = (uint)depth;
        
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        desc.label              = WgpuUtils.CopyToStringView(label, labelBuffer, labelMaxCount);
        
        fixed(TextureFormat* ptr = viewFormats) {
            desc.viewFormats = ptr;
            Texture* texture = wgpuDeviceCreateTexture(DevicePtr, &desc);
            return new GpuTextureStorage3D(this, desc, texture, label);
        }
    }

#endregion
}