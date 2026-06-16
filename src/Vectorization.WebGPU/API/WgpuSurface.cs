// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;

// --- Windows
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SurfaceDescriptorFromWindowsHWND
{
    public ChainedStruct    chain;
    public void*            hinstance;
    public void*            hwnd;
}

// --- macOS
[StructLayout(LayoutKind.Sequential)]
public unsafe struct SurfaceDescriptorFromMetalLayer {
    public ChainedStruct    chain;
    public void*            layer; // CAMetalLayer (metalLayer)
}

public readonly unsafe struct WgpuSurface(Surface* handle)
{
    internal readonly   Surface*  handle = handle;
    
    public void Present() {
        wgpuSurfacePresent(handle);
    }
    
    public TextureFormat GetSwapChainFormat(WgpuAdapter adapter)
    {
        var capabilities = new SurfaceCapabilities();
        wgpuSurfaceGetCapabilities(handle, adapter.adapter, &capabilities);
        return capabilities.formats[0];
    }
    
    /// <remarks>
    /// Typical configuration
    /// <code>
    ///     var surfaceConfig = new SurfaceConfiguration {
    ///         format      = TextureFormat.BGRA8Unorm,     // supported by most devices
    ///         usage       = WebGPU_native.TextureUsage_RenderAttachment,
    ///         alphaMode   = CompositeAlphaMode.Opaque,
    ///         width       = (uint)pixelWidth,
    ///         height      = (uint)pixelHeight,
    ///         presentMode = PresentMode.Fifo              // Fifo = VSync
    ///     };
    /// </code>
    /// </remarks>
    public void Configure(GpuDevice device, SurfaceConfiguration surfaceConfig)
    {
        var wgpuDevice = (WgpuDevice)device;
        surfaceConfig.device = wgpuDevice.DevicePtr;
        // surfaceConfig.format = TextureFormat.BGRA8Unorm;    - standard supported by most devices
        // or: retrieve TextureFormat via   wgpuSurfaceGetCapabilities(surface.handle, adapter.handle, ...)
        
        wgpuSurfaceConfigure(handle, &surfaceConfig);
    }
    
    
    public static WgpuSurface CreateFromNativeWindow(WgpuInstance instance, nint hwnd, nint hInstance)
    {
        if (OperatingSystem.IsWindows()) {
            return CreateFromHwnd (instance, hwnd, hInstance);
        }
        if (OperatingSystem.IsMacOS()) {
            return SurfaceDescriptorFromCocoaWindow (instance, hwnd);
        }
        throw new NotImplementedException($"not code to get WgpuSurface for OS: {RuntimeInformation.OSDescription}");
    }
    
    public static WgpuSurface CreateFromHwnd(WgpuInstance instance, nint hwnd, nint hInstance)
    {
        var winDesc = new SurfaceDescriptorFromWindowsHWND {
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
        nint contentViewSelector = MacNative.SelRegisterName("contentView");
        nint nsView = MacNative.ObjCMsgSend(nsWindow, contentViewSelector);
        
        nint metalLayer = MacNative.PrepareNsViewForWgpu(nsView);
        
        var macDesc = new SurfaceDescriptorFromMetalLayer {
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




public static class MacNative
{
    public static IntPtr PrepareNsViewForWgpu(IntPtr nsView)
    {
        if (nsView == IntPtr.Zero) return IntPtr.Zero;

        nint setWantsLayerSel = SelRegisterName("setWantsLayer:");
        ObjCMsgSend_Bool(nsView, setWantsLayerSel, true);

        nint caMetalLayerClass = ObjCGetClass("CAMetalLayer");
        if (caMetalLayerClass == IntPtr.Zero) {
            nint dl = dlopen("/System/Library/Frameworks/QuartzCore.framework/QuartzCore", 1);
            caMetalLayerClass = ObjCGetClass("CAMetalLayer");
        }
        nint allocSel = SelRegisterName("alloc");
        nint initSel = SelRegisterName("init");
    
        nint metalLayerAlloc = ObjCMsgSend(caMetalLayerClass, allocSel);
        nint metalLayer = ObjCMsgSend(metalLayerAlloc, initSel);

        if (metalLayer == IntPtr.Zero) {
            throw new Exception("failed to initialize CAMetalLayer");
        }
        nint setLayerSel = SelRegisterName("setLayer:");
        ObjCMsgSend_IntPtr(nsView, setLayerSel, metalLayer);

        return metalLayer; 
    }
    
    /* public static IntPtr GetMacOsNsView(nint nsWindow)
    {
        IntPtr contentViewSelector = SelRegisterName("contentView");

        IntPtr nsView = ObjCMsgSend(nsWindow, contentViewSelector);

        if (nsView == IntPtr.Zero) {
            throw new Exception("contentView of NSView is Null.");
        }
        return nsView;
    } */
    
    private const string ObjCRuntime = "/usr/lib/libobjc.A.dylib";

    
    
    [DllImport(ObjCRuntime, EntryPoint = "sel_registerName")]
    public static extern IntPtr SelRegisterName(string name);

    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    public static extern IntPtr ObjCMsgSend(IntPtr receiver, IntPtr selector);
    
    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    public static extern void ObjCMsgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);
    
    // overload for boolean parameter (required for setWantsLayer:)
    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    public static extern void ObjCMsgSend_Bool(IntPtr receiver, IntPtr selector, bool value);

    // used to find CAMetalLayer class
    [DllImport(ObjCRuntime, EntryPoint = "objc_getClass")]
    public static extern IntPtr ObjCGetClass(string name);
    
    [DllImport("libdl.dylib")]
    private static extern IntPtr dlopen(string filename, int flags);
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