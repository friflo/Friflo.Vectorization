// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


// --- windows
[StructLayout(LayoutKind.Sequential)]
public struct WgpuSurfaceDescriptorFromWindowsHWND
{
    public ChainedStruct    chain;
    public nint             hinstance;
    public nint             hwnd;
}


public readonly unsafe partial struct WgpuSurface
{
    private static WgpuSurface CreateFromHwnd(WgpuInstance instance, nint hwnd, nint hInstance)
    {
        var winDesc = new WgpuSurfaceDescriptorFromWindowsHWND {
            chain = new ChainedStruct {
                next  = null,
                sType = SType.SurfaceSourceWindowsHWND
            },
            hinstance = hInstance,
            hwnd      = hwnd
        };
        var surfaceDesc = new SurfaceDescriptor {
            label       = default,
            nextInChain = (ChainedStruct*)&winDesc
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
