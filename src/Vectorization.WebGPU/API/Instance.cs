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

    public static WgpuInstance CreateInstance(InstanceExtras instanceExtras)
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
    
    public WgpuAdapter RequestAdapter(RequestAdapterOptions options, WgpuAdapterInfo adapterInfo)
    {
		Adapter* adapter = null;
        if (adapterInfo != null) {
            adapter = adapterInfo.Adapter;
        } else {
            var callbackInfo = new RequestAdapterCallbackInfo {
                mode        = CallbackMode.WaitAnyOnly,
                callback    = &RequestAdapter_callback,
                userdata1   = &adapter
            };
		    var future = wgpuInstanceRequestAdapter(instance, &options, callbackInfo);
            if (future.id != 0) {
                var waitInfo = new FutureWaitInfo { future = future, completed = 0 };
                wgpuInstanceWaitAny(instance, 1, &waitInfo, 2000);
            }
            if (adapter == null) {
                throw new Exception("Failed to create WebGPU Adapter. Status: ");
            }
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
        var info = WgpuAdapterInfo.CreateAdapterInfo(props, adapter);
        return new WgpuAdapter(adapter, instance, info);
    }
    
    public GlobalReport GenerateReport () {
        var report = new GlobalReport();
        wgpuGenerateReport(instance, &report);
        return report;
    }
    
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
    
    public override WgpuAdapterInfo[] GetAdapterInfos()
    {
        InstanceEnumerateAdapterOptions options = default;
        nuint adapterCount = wgpuInstanceEnumerateAdapters(instance, &options, null);
        var infos = new WgpuAdapterInfo[adapterCount];
        
        Adapter** adapters = stackalloc Adapter*[ (int)adapterCount ];
        wgpuInstanceEnumerateAdapters(instance, &options, adapters);
        for (int i = 0; i < (int)adapterCount; i++)
        {
            Adapter* adapter = adapters[i];
            AdapterInfo info = default;
            wgpuAdapterGetInfo(adapter, &info);
            infos[i] = WgpuAdapterInfo.CreateAdapterInfo(info, adapter);
        }
        return infos;
    }
}

