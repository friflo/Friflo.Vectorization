// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
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
public unsafe struct SurfaceDescriptorFromCocoaWindow {
    public ChainedStruct    chain;
    public void*            nsWindow;
}

public readonly unsafe struct WgpuSurface(Surface* handle)
{
    internal readonly   Surface*  handle = handle;
    
    public void Present() {
        wgpuSurfacePresent(handle);
    }
    
    public void Configure(WgpuDevice device, int width, int height)
    {
        // WebGPU-Standard fo most monitors: BGRA8Unorm
        // Better: retrieve TextureFormat via   wgpuSurfaceGetCapabilities(surface.handle, adapter.handle, ...)
        var config = new SurfaceConfiguration {
            nextInChain     = null,
            device          = device.DevicePtr,
            format          = TextureFormat.BGRA8Unorm,  // must be same as in   RenderTest.Triangles_GPU_CreateEffect()
            usage           = TextureUsage_RenderAttachment,
            viewFormatCount = 0,
            viewFormats     = null,
            alphaMode       = CompositeAlphaMode.Opaque,
            width           = (uint)width,
            height          = (uint)height,
            presentMode     = PresentMode.Fifo // corresponds to VSync (Standard)
        };
        wgpuSurfaceConfigure(handle, &config);
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
        var macDesc = new SurfaceDescriptorFromCocoaWindow {
            chain = new ChainedStruct {
                next  = null,
                sType = SType.SurfaceSourceMetalLayer
            },
            nsWindow = (void*)nsWindow
        };
        var surfaceDesc = new SurfaceDescriptor {
            label       = default,
            nextInChain = (ChainedStruct*)&macDesc
        };
    
        var surfaceHandle = wgpuInstanceCreateSurface(instance.instance, &surfaceDesc);
    
        return new WgpuSurface(surfaceHandle);
    }
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