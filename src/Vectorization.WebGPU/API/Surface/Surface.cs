// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable UnassignedField.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


/// <summary> managed type for:  <see cref="SurfaceConfiguration"/>. </summary>
public struct WgpuSurfaceConfiguration
{
    public  nint                nextInChain;
    public  GpuDevice           device;
    public  TextureFormat       format;
    public  TextureUsage        usage;
    public  int                 width;
    public  int                 height;
    public  TextureFormat[]     viewFormats;
    public  CompositeAlphaMode  alphaMode;
    public  PresentMode         presentMode;
}


/// <summary> Managed handle for: <see cref="Surface"/>. </summary>
public readonly unsafe partial struct WgpuSurface : IDisposable
{
    internal readonly   Surface*  handle;
    
    internal WgpuSurface(Surface* handle) {
        this.handle = handle;
    }

    public void Dispose()
    {
        wgpuSurfaceRelease(handle);
    }
    
    public void Present() {
        wgpuSurfacePresent(handle);
    }
    
    /// <summary> Used to return the optimal <see cref="GpuFragmentState"/> for your adapter. </summary>
    /// <remarks>
    /// Intended usage
    /// <code>
    ///     var fragmentState   = Surface.GetPreferredFragmentState(Adapter, true, out AlphaMode);
    ///     var swapChainFormat = fragmentState.targets[0].format; 
    ///     var desc            = new GpuRenderPipelineDescriptor { FragmentState = fragmentState };
    ///     var config          = desc.CreateConfig("render config");
    /// </code>
    /// Use <c>swapChainFormat</c> in the <c>SurfaceConfiguration</c> passed to <see cref="Configure"/>.
    /// </remarks>
	public GpuFragmentState GetPreferredFragmentState(GpuAdapter adapter, bool useNonSrgb, out CompositeAlphaMode alphaMode)
    {
        var capabilities = new SurfaceCapabilities();
        var wgpuAdapter = (WgpuAdapter)adapter;
        wgpuSurfaceGetCapabilities(handle, wgpuAdapter.adapter, &capabilities);
        var format = capabilities.formatCount > 0 ? capabilities.formats[0] : TextureFormat.BGRA8Unorm;
        if (useNonSrgb) {
            format = ToNonSrgb(format);
        }
        alphaMode = capabilities.alphaModeCount > 0 ? capabilities.alphaModes[0] : CompositeAlphaMode.Opaque;
        wgpuSurfaceCapabilitiesFreeMembers(capabilities);
        return new GpuFragmentState { targets = [new GpuColorTargetState { format = format }] };
    }
    
    private static TextureFormat ToNonSrgb(TextureFormat format)
    {
        return format switch
        {
            TextureFormat.RGBA8UnormSrgb        => TextureFormat.RGBA8Unorm,
            TextureFormat.BGRA8UnormSrgb        => TextureFormat.BGRA8Unorm,
            TextureFormat.BC1RGBAUnormSrgb      => TextureFormat.BC1RGBAUnorm,
            TextureFormat.BC2RGBAUnormSrgb      => TextureFormat.BC2RGBAUnorm,
            TextureFormat.BC3RGBAUnormSrgb      => TextureFormat.BC3RGBAUnorm,
            TextureFormat.BC7RGBAUnormSrgb      => TextureFormat.BC7RGBAUnorm,
            TextureFormat.ETC2RGB8UnormSrgb     => TextureFormat.ETC2RGB8Unorm,
            TextureFormat.ETC2RGB8A1UnormSrgb   => TextureFormat.ETC2RGB8A1Unorm,
            TextureFormat.ETC2RGBA8UnormSrgb    => TextureFormat.ETC2RGBA8Unorm,
            TextureFormat.ASTC4x4UnormSrgb      => TextureFormat.ASTC4x4Unorm,
            TextureFormat.ASTC5x4UnormSrgb      => TextureFormat.ASTC5x4Unorm,
            TextureFormat.ASTC5x5UnormSrgb      => TextureFormat.ASTC5x5Unorm,
            TextureFormat.ASTC6x5UnormSrgb      => TextureFormat.ASTC6x5Unorm,
            TextureFormat.ASTC6x6UnormSrgb      => TextureFormat.ASTC6x6Unorm,
            TextureFormat.ASTC8x5UnormSrgb      => TextureFormat.ASTC8x5Unorm,
            TextureFormat.ASTC8x6UnormSrgb      => TextureFormat.ASTC8x6Unorm,
            TextureFormat.ASTC8x8UnormSrgb      => TextureFormat.ASTC8x8Unorm,
            TextureFormat.ASTC10x5UnormSrgb     => TextureFormat.ASTC10x5Unorm,
            TextureFormat.ASTC10x6UnormSrgb     => TextureFormat.ASTC10x6Unorm,
            TextureFormat.ASTC10x8UnormSrgb     => TextureFormat.ASTC10x8Unorm,
            TextureFormat.ASTC10x10UnormSrgb    => TextureFormat.ASTC10x10Unorm,
            TextureFormat.ASTC12x10UnormSrgb    => TextureFormat.ASTC12x10Unorm,
            TextureFormat.ASTC12x12UnormSrgb    => TextureFormat.ASTC12x12Unorm,
            _                                   => format
        };
    }
    
    /// <remarks>
    /// Typical configuration
    /// <code>
    ///     var surfaceConfig = new SurfaceConfiguration {
    ///         format      = swapChainFormat,      // see: GpuSurface.GetPreferredFragmentState()
    ///         usage       = WebGPU_native.TextureUsage_RenderAttachment,
    ///         alphaMode   = CompositeAlphaMode.Opaque,
    ///         width       = (uint)pixelWidth,
    ///         height      = (uint)pixelHeight,
    ///         presentMode = PresentMode.Fifo      // Fifo = VSync
    ///     };
    /// </code>
    /// Get <c>swapChainFormat</c> via <see cref="GetPreferredFragmentState"/>.
    /// </remarks>
    public void Configure(in WgpuSurfaceConfiguration surfaceConfig)
    {
        var wgpuDevice = (WgpuDevice)surfaceConfig.device;
        var config = new SurfaceConfiguration {
            nextInChain     = (ChainedStruct*)surfaceConfig.nextInChain,
            device          = wgpuDevice.DevicePtr,
            format          = surfaceConfig.format,
            usage           = (ulong)surfaceConfig.usage,
            width           = (uint)surfaceConfig.width,
            height          = (uint)surfaceConfig.height,
            viewFormatCount = (uint)(surfaceConfig.viewFormats?.Length ?? 0),
            alphaMode       = surfaceConfig.alphaMode,
            presentMode     = surfaceConfig.presentMode
        };
        // surfaceConfig.format = TextureFormat.BGRA8Unorm;    - standard supported by most devices
        // or: retrieve TextureFormat via   wgpuSurfaceGetCapabilities(surface.handle, adapter.handle, ...)
        
        fixed(TextureFormat* textureFormats = surfaceConfig.viewFormats)
        {
            config.viewFormats = textureFormats;
            wgpuSurfaceConfigure(handle, &config);    
        }
    }
    
    public void Unconfigure()
    {
        wgpuSurfaceUnconfigure(handle);
    }
    
    public static WgpuSurface CreateFromNativeWindow(WgpuInstance instance, nint hwnd, nint hInstance)
    {
        if (OperatingSystem.IsWindows()) {
            return CreateFromHwnd (instance, hwnd, hInstance);
        }
        if (OperatingSystem.IsMacOS()) {
            return SurfaceDescriptorFromCocoaWindow (instance, hwnd);
        }
        throw new NotImplementedException($"no code to get WgpuSurface for OS: {RuntimeInformation.OSDescription}");
    }
    
    private static T[] ToArray<T>(nuint count, T* ptr) where T : unmanaged {
        if (count == 0) {
            return [];
        }
        var arr = new T[count];
        for (int i = 0; i < (int)count; i++) {
            arr[i] = ptr[i];
        }
        return arr;
    }
    
    public WgpuSurfaceCapabilities GetSurfaceCapabilities(WgpuAdapter adapter)
    {
        var cap = new SurfaceCapabilities();
        wgpuSurfaceGetCapabilities(handle, adapter.adapter, &cap);
        var capabilities = new WgpuSurfaceCapabilities(
            cap.usages,
            ToArray(cap.formatCount,        cap.formats),
            ToArray(cap.presentModeCount,   cap.presentModes),
            ToArray(cap.alphaModeCount,     cap.alphaModes));
        wgpuSurfaceCapabilitiesFreeMembers(cap);
        return capabilities;
    }
}


/// <summary> Managed type for <see cref="SurfaceCapabilities"/> </summary>
public readonly struct WgpuSurfaceCapabilities
{
    public readonly TextureUsage            usages;
    public readonly TextureFormat[]         formats;
    public readonly PresentMode[]           presentModes;
    public readonly CompositeAlphaMode[]    alphaModes;
    
    internal WgpuSurfaceCapabilities(ulong usages, TextureFormat[] formats, PresentMode[] presentModes, CompositeAlphaMode[] alphaModes)
    {
        this.usages         = (TextureUsage)usages;
        this.formats        = formats;
        this.presentModes   = presentModes;
        this.alphaModes     = alphaModes;
    }
}
