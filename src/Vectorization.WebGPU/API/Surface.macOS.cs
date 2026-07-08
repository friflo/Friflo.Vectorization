// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;


// ReSharper disable CheckNamespace
namespace Friflo.Vectorization.WebGPU;


// --- macOS
[StructLayout(LayoutKind.Sequential)]
public struct WgpuSurfaceDescriptorFromMetalLayer {
    public ChainedStruct    chain;
    public nint             layer; // CAMetalLayer (metalLayer)
}


public readonly unsafe partial struct WgpuSurface
{
    private static WgpuSurface SurfaceDescriptorFromCocoaWindow(WgpuInstance instance, nint nsWindow)
    {
        nint contentViewSelector    = MacNative.SelRegisterName("contentView");
        nint nsView                 = MacNative.ObjCMsgSend(nsWindow, contentViewSelector);
        nint metalLayer             = MacNative.PrepareNsViewForWgpu(nsView);
        
        var macDesc = new WgpuSurfaceDescriptorFromMetalLayer {
            chain = new ChainedStruct {
                next  = null,
                sType = SType.SurfaceSourceMetalLayer
            },
            layer = metalLayer,
        };
        var surfaceDesc = new SurfaceDescriptor {
            label       = default,
            nextInChain = (ChainedStruct*)&macDesc
        };
    
        var surfaceHandle = wgpuInstanceCreateSurface(instance.instance, &surfaceDesc);
    
        return new WgpuSurface(surfaceHandle);
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
    private static extern void ObjCMsgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr argument);
    
    // overload for boolean parameter (required for setWantsLayer:)
    [DllImport(ObjCRuntime, EntryPoint = "objc_msgSend")]
    private static extern void ObjCMsgSend_Bool(IntPtr receiver, IntPtr selector, bool value);

    // used to find CAMetalLayer class
    [DllImport(ObjCRuntime, EntryPoint = "objc_getClass")]
    private static extern IntPtr ObjCGetClass(string name);
    
    [DllImport("libdl.dylib")]
    internal static extern IntPtr dlopen(string filename, int flags);
}
