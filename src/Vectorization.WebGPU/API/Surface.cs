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

// --- Windows
[StructLayout(LayoutKind.Sequential)]
public unsafe struct WgpuSurfaceDescriptorFromWindowsHWND
{
    public ChainedStruct    chain;
    public void*            hinstance;
    public void*            hwnd;
}

// --- macOS
[StructLayout(LayoutKind.Sequential)]
public unsafe struct WgpuSurfaceDescriptorFromMetalLayer {
    public ChainedStruct    chain;
    public void*            layer; // CAMetalLayer (metalLayer)
}

public readonly unsafe struct WgpuSurface : IDisposable
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
    
    public WgpuSurfaceCapabilities GetSurfaceCapabilities(WgpuAdapter adapter)
    {
        var cap = new SurfaceCapabilities();
        wgpuSurfaceGetCapabilities(handle, adapter.adapter, &cap);
        var capabilities = new WgpuSurfaceCapabilities( cap.usages,
            ToArray(cap.formatCount,        cap.formats),
            ToArray(cap.presentModeCount,   cap.presentModes),
            ToArray(cap.alphaModeCount,     cap.alphaModes));
        wgpuSurfaceCapabilitiesFreeMembers(cap);
        return capabilities;
    }
    
    /// <summary> Used to return the optimal <see cref="GpuFragmentState"/> for your adapter. </summary>
    /// <remarks>
    /// Intended usage
    /// <code>
    ///     var fragmentState   = surface.GetPreferredFragmentState(adapter, true);
    ///     var swapChainFormat = fragmentState.targets[0].format; 
    ///     var desc            = new WgpuRenderPipelineDescriptor { FragmentState = fragmentState };
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
    
    /// <remarks>
    /// Typical configuration
    /// <code>
    ///     var surfaceConfig = new SurfaceConfiguration {
    ///         format      = swapChainFormat,      // see: WgpuSurface.GetPreferredFragmentState()
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
    
    public static WgpuSurface CreateFromHwnd(WgpuInstance instance, nint hwnd, nint hInstance)
    {
        var winDesc = new WgpuSurfaceDescriptorFromWindowsHWND {
            chain = new ChainedStruct {
                next  = null,
                sType = SType.SurfaceSourceWindowsHWND
            },
            hinstance = (void*)hInstance,
            hwnd      = (void*)hwnd
        };
        var surfaceDesc = new SurfaceDescriptor {
            label       = default,
            nextInChain = (ChainedStruct*)&winDesc
        };
        var surfaceHandle = wgpuInstanceCreateSurface(instance.instance, &surfaceDesc);
        
        return new WgpuSurface(surfaceHandle);
    }
    
    public static WgpuSurface SurfaceDescriptorFromCocoaWindow(WgpuInstance instance, nint nsWindow)
    {
        nint contentViewSelector    = MacNative.SelRegisterName("contentView");
        nint nsView                 = MacNative.ObjCMsgSend(nsWindow, contentViewSelector);
        nint metalLayer             = MacNative.PrepareNsViewForWgpu(nsView);
        
        var macDesc = new WgpuSurfaceDescriptorFromMetalLayer {
            chain = new ChainedStruct {
                next  = null,
                sType = SType.SurfaceSourceMetalLayer
            },
            layer    = (void*)metalLayer,
        };
        var surfaceDesc = new SurfaceDescriptor {
            label       = default,
            nextInChain = (ChainedStruct*)&macDesc
        };
    
        var surfaceHandle = wgpuInstanceCreateSurface(instance.instance, &surfaceDesc);
    
        return new WgpuSurface(surfaceHandle);
    }
}

/// <summary> Managed type for <see cref="SurfaceCapabilities"/> </summary>
public readonly struct WgpuSurfaceCapabilities
{
    public readonly ulong                   usages;
    public readonly TextureFormat[]         formats;
    public readonly PresentMode[]           presentModes;
    public readonly CompositeAlphaMode[]    alphaModes;
    
    internal WgpuSurfaceCapabilities(ulong usages, TextureFormat[] formats, PresentMode[] presentModes, CompositeAlphaMode[] alphaModes)
    {
        this.usages         = usages;
        this.formats        = formats;
        this.presentModes   = presentModes;
        this.alphaModes     = alphaModes;
    }
}


internal static class MacNative
{
    internal static IntPtr PrepareNsViewForWgpu(IntPtr nsView)
    {
        if (nsView == IntPtr.Zero) return IntPtr.Zero;

        nint setWantsLayerSel = SelRegisterName("setWantsLayer:");
        ObjCMsgSend_Bool(nsView, setWantsLayerSel, true);

        nint caMetalLayerClass = ObjCGetClass("CAMetalLayer");
        /* if (caMetalLayerClass == IntPtr.Zero) {
            nint dl = dlopen("/System/Library/Frameworks/QuartzCore.framework/QuartzCore", 1);
            caMetalLayerClass = ObjCGetClass("CAMetalLayer");
        } */
        nint allocSel   = SelRegisterName("alloc");
        nint initSel    = SelRegisterName("init");
    
        nint metalLayerAlloc    = ObjCMsgSend(caMetalLayerClass, allocSel);
        nint metalLayer         = ObjCMsgSend(metalLayerAlloc, initSel);

        if (metalLayer == IntPtr.Zero) {
            throw new Exception("failed to initialize CAMetalLayer");
        }
        nint setLayerSel = SelRegisterName("setLayer:");
        ObjCMsgSend_IntPtr(nsView, setLayerSel, metalLayer);

        return metalLayer; 
    }
    
    
    private const string ObjCRuntime = "/usr/lib/libobjc.A.dylib";
    
    [DllImport(ObjCRuntime, EntryPoint = "sel_registerName")]
    internal static extern IntPtr SelRegisterName(string name);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    internal static extern IntPtr ObjCMsgSend(IntPtr receiver, IntPtr selector);
    
    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    internal static extern void ObjCMsgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);
    
    // overload for boolean parameter (required for setWantsLayer:)
    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    internal static extern void ObjCMsgSend_Bool(IntPtr receiver, IntPtr selector, bool value);

    // used to find CAMetalLayer class
    [DllImport(ObjCRuntime, EntryPoint = "objc_getClass")]
    internal static extern IntPtr ObjCGetClass(string name);
    
    [DllImport("libdl.dylib")]
    internal static extern IntPtr dlopen(string filename, int flags);
}

/*
//  Usage:
// var hInstance   = Windowing.GetModuleHandleW(null);
// var hwnd        = Windowing.CreateWindowExW(0, "Static", "wgpu", 0x10CF0000, 100, 100, width, height, 0, 0, hInstance, 0);

public static unsafe class Windowing
{
    public static nint CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, 
        nint hWndParent, nint hMenu, nint hInstance, nint lpParam)
    {
        return (nint)Win32Native.CreateWindowExW(dwExStyle, lpClassName, lpWindowName, dwStyle, x, y, nWidth, nHeight,
            (void*)hWndParent, (void*)hMenu, (void*)hInstance, (void*)lpParam);
    }

    public static nint GetModuleHandleW(string lpModuleName) => (nint)Win32Native.GetModuleHandleW(lpModuleName);
}

internal static unsafe class Win32Native
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern void* CreateWindowExW(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, 
        void* hWndParent, void* hMenu, void* hInstance, void* lpParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern void* GetModuleHandleW(string lpModuleName);
}
*/