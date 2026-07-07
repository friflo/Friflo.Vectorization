// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

/*
    Important note for Dispose pattern
    ----------------------------------


    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);  // prevent execution of finalizer WHEN Dispose() is called manually
    }
    
    // A finalizer can be call from any thread.
    ~GpuClass() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool disposing)
    {
        if (isDisposed) return;  // guarantees this block is executed only once

        // Other managed objects MUST not be touched if disposing == false.
        if (disposing) {
            // cleanup up managed resources
            ...
        }
        // Release native resources. Order matters
        // Native pointer MUST be checked for null. Their creation may have failed
        if (deviceHandle.IsAllocated) {
            deviceHandle.Free();
        }
        isDisposed = true;
    }

 */


public struct GpuRequestAdapterOptions
{
    public  nint            nextInChain;
    public  FeatureLevel    featureLevel;
    public  PowerPreference powerPreference;
    public  uint            forceFallbackAdapter;
    public  BackendType     backendType;
    public  WgpuSurface     compatibleSurface;
}

public sealed unsafe class WgpuInstance : GpuInstance
{
    internal readonly   Instance*   instance;
    private             bool        isDisposed;
    public   override   bool        IsDisposed => isDisposed;
    
    public   override    string      ToString() => isDisposed ? "Disposed" : "Alive";
    
    private WgpuInstance(Instance* instance)
    {
        this.instance   = instance;
    }
    
    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~WgpuInstance() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool _)
    {
        if (isDisposed) return;
        if (instance != null) {
            wgpuInstanceRelease(instance);
        }        
        isDisposed = true;
    }

    public static WgpuInstance CreateInstance(InstanceExtras instanceExtras = default)
    {
        var extras  = instanceExtras;
        
        const SType wgpuSTypeInstanceExtras = (SType)0x60000001;
        extras.chain = new ChainedStruct {
            sType = wgpuSTypeInstanceExtras
        };
        extras.chain.next = null;
        
		var instDesc = new InstanceDescriptor {
            nextInChain = (ChainedStruct*)&extras
        };
		var instance = wgpuCreateInstance(&instDesc);
        if (instance == null) {
            throw new Exception("The Void Stares Back: Failed to create GpuInstance. Check your drivers!");
        }
        return new WgpuInstance(instance);
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void RequestAdapter_callback(RequestAdapterStatus status, Adapter* adapter, StringView message, void* userdata1, void* userdata2)
    {
        if (userdata1 == null) return;
        var adapterPtr = (Adapter**)userdata1;
        *adapterPtr = adapter;
    }
    
    public WgpuAdapter RequestAdapter(in GpuRequestAdapterOptions options)
    {
		Adapter* adapter = null;
        var callbackInfo = new RequestAdapterCallbackInfo {
            mode        = CallbackMode.WaitAnyOnly,
            callback    = &RequestAdapter_callback,
            userdata1   = &adapter
        };
        var opt = new RequestAdapterOptions {
            nextInChain             = (ChainedStruct*)options.nextInChain,
            featureLevel            = options.featureLevel,
            powerPreference         = options.powerPreference,
            forceFallbackAdapter    = options.forceFallbackAdapter,
            backendType             = options.backendType,
            compatibleSurface       = options.compatibleSurface.handle
        };
		var future = wgpuInstanceRequestAdapter(instance, &opt, callbackInfo);
        if (future.id != 0) {
            var waitInfo = new FutureWaitInfo { future = future, completed = 0 };
            wgpuInstanceWaitAny(instance, 1, &waitInfo, 2000);
        }
        if (adapter == null) {
            throw new Exception("Failed to create WebGPU Adapter. Status: ");
        }

        /* var startTime = Stopwatch.StartNew();
        var timeOutMs = 1000;
        while (adapter == null) {
            PumpEvents(instance);
            if (startTime.ElapsedMilliseconds > timeOutMs) throw new TimeoutException("While requesting adapter");
        }
        if (adapter == null) {
            Console.WriteLine("Adapter-Timeout: driver was found. but no callback was fired");
        } */
        var props = new AdapterInfo();
        wgpuAdapterGetInfo(adapter, &props);
        var info = WgpuAdapterInfo.CreateAdapterInfo(props);
        wgpuAdapterInfoFreeMembers(props);
        return new WgpuAdapter(adapter, instance, info);
    }

    // obsolete - kept for reference
    internal static void PumpEvents(Instance* instance)
    {
        wgpuInstanceProcessEvents(instance);   // not relevant

        // This check is required when running on Linux using only Software GPU
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            var enumOptions = new InstanceEnumerateAdapterOptions();
            Adapter* dummyAdapter = null;
            // Trigger processing pending callbacks
            wgpuInstanceEnumerateAdapters(instance, &enumOptions, &dummyAdapter);
            Thread.Yield(); // enable other threads on Linux processing events 
        }
    }
    
    private static GpuHandleDiff GpuHandles(in GlobalReport globalReport)
    {
        var hub         = globalReport.hub;
        var surfaces    = globalReport.surfaces;
        return new GpuHandleDiff {
            Adapters            = new GpuHandle((long)hub.adapters.           numKeptFromUser),
            Devices             = new GpuHandle((long)hub.devices.            numKeptFromUser),
            Queues              = new GpuHandle((long)hub.queues.             numKeptFromUser),
            PipelineLayouts     = new GpuHandle((long)hub.pipelineLayouts.    numKeptFromUser),
            ShaderModules       = new GpuHandle((long)hub.shaderModules.      numKeptFromUser),
            BindGroupLayouts    = new GpuHandle((long)hub.bindGroupLayouts.   numKeptFromUser),
            BindGroups          = new GpuHandle((long)hub.bindGroups.         numKeptFromUser),
            CommandBuffers      = new GpuHandle((long)hub.commandBuffers.     numKeptFromUser),
            RenderBundles       = new GpuHandle((long)hub.renderBundles.      numKeptFromUser),
            RenderPipelines     = new GpuHandle((long)hub.renderPipelines.    numKeptFromUser),
            ComputePipelines    = new GpuHandle((long)hub.computePipelines.   numKeptFromUser),
            PipelineCaches      = new GpuHandle((long)hub.pipelineCaches.     numKeptFromUser),
            QuerySets           = new GpuHandle((long)hub.querySets.          numKeptFromUser),
            Buffers             = new GpuHandle((long)hub.buffers.            numKeptFromUser),
            Textures            = new GpuHandle((long)hub.textures.           numKeptFromUser),
            TextureViews        = new GpuHandle((long)hub.textureViews.       numKeptFromUser),
            Samplers            = new GpuHandle((long)hub.samplers.           numKeptFromUser),
            Surfaces            = new GpuHandle((long)surfaces.               numKeptFromUser),
        };
    }
    
    /// <summary> Returns all <see cref="WgpuAdapter"/>'s. <c>Dispose()</c> adapters to prevent leaks. </summary>
    public WgpuAdapter[] GetAdapters()
    {
        InstanceEnumerateAdapterOptions options = default;
        nuint adapterCount = wgpuInstanceEnumerateAdapters(instance, &options, null);
        var wgpuAdapters = new WgpuAdapter[adapterCount];
        
        Adapter** adapters = stackalloc Adapter*[ (int)adapterCount ];
        wgpuInstanceEnumerateAdapters(instance, &options, adapters); // creates Adapter* handles
        for (int i = 0; i < (int)adapterCount; i++)
        {
            Adapter* adapter = adapters[i];
            AdapterInfo info = default;
            wgpuAdapterGetInfo(adapter, &info);
            var wgpuInfo = WgpuAdapterInfo.CreateAdapterInfo(info);
            wgpuAdapterInfoFreeMembers(info);
            wgpuAdapters[i] = new WgpuAdapter(adapter, instance, wgpuInfo);
        }
        return wgpuAdapters;
    }
    
    public override WgpuAdapterInfo[] GetAdapterInfos()
    {
        InstanceEnumerateAdapterOptions options = default;
        nuint adapterCount = wgpuInstanceEnumerateAdapters(instance, &options, null);
        var infos = new WgpuAdapterInfo[adapterCount];
        
        Adapter** adapters = stackalloc Adapter*[ (int)adapterCount ];
        wgpuInstanceEnumerateAdapters(instance, &options, adapters); // creates Adapter* handles
        for (int i = 0; i < (int)adapterCount; i++)
        {
            Adapter* adapter = adapters[i];
            AdapterInfo info = default;
            wgpuAdapterGetInfo(adapter, &info);
            infos[i] = WgpuAdapterInfo.CreateAdapterInfo(info);
            wgpuAdapterInfoFreeMembers(info);
            wgpuAdapterRelease(adapter);
        }
        return infos;
    }
    
    public override GpuHandleDiff GenerateHandles () {
        var globalReport = new GlobalReport();
        wgpuGenerateReport(instance, &globalReport);
        return GpuHandles(globalReport);
    }
}

