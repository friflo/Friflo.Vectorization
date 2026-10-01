// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Friflo.WGPU.Runtime;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.WGPU;


// --- linux
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WgpuSurfaceDescriptorFromWaylandSurface
{
    internal    ChainedStruct   chain;
    internal    void*           display; // wl_display*
    internal    void*           surface; // wl_surface*
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WgpuSurfaceDescriptorFromXlibWindow
{
    internal    ChainedStruct   chain;
    internal    void*           display; // Display*
    internal    ulong           window;  // Window (XID)
}


public readonly unsafe partial struct WgpuSurface
{
    private static WgpuSurface CreateFromWaylandSurface(WgpuInstance instance, nint display, nint surface)
    {
        var waylandDesc = new WgpuSurfaceDescriptorFromWaylandSurface {
            chain = new ChainedStruct {
                next  = null,
                sType = SType.SurfaceSourceWaylandSurface
            },
            display = (void*)display, // wl_display*
            surface = (void*)surface  // wl_surface*
        };
        var surfaceDesc = new SurfaceDescriptor {
            label       = default,
            nextInChain = (ChainedStruct*)&waylandDesc
        };
        var surfaceHandle = wgpuInstanceCreateSurface(instance.instance, &surfaceDesc);

        return new WgpuSurface(surfaceHandle);
    }
    
    private static WgpuSurface CreateFromXlibWindow(WgpuInstance instance, nint display, nint window)
    {
        var xlibDesc = new WgpuSurfaceDescriptorFromXlibWindow {
            chain = new ChainedStruct {
                next  = null,
                sType = SType.SurfaceSourceXlibWindow
            },
            display = (void*)display,     // Display*
            window  = (ulong)window       // Window (XID)
        };
        var surfaceDesc = new SurfaceDescriptor {
            label       = default,
            nextInChain = (ChainedStruct*)&xlibDesc
        };
        var surfaceHandle = wgpuInstanceCreateSurface(instance.instance, &surfaceDesc);

        return new WgpuSurface(surfaceHandle);
    }
}

