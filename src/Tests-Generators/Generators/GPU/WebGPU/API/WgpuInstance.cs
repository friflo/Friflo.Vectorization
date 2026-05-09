// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WGPU;

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


public sealed unsafe class WgpuInstance : IDisposable
{
    private readonly    WebGPU      wgpu;
    private readonly    Wgpu        wgpuEx;
    private readonly    Instance*   instance;
    private             bool        isDisposed;
    public              bool        IsDisposed => isDisposed;
    
    public  override    string      ToString() => isDisposed ? "Disposed" : "Alive";
    
    // IMPORTANT: WebGPU and Wgpu classes are referenced with static readonly fields.
    // Reason:  Gpu classes use finalizers to release native resources.
    //          Since the GC does not guarantee the order of finalization,
    //          the managed API wrappers (WebGPU/Wgpu) could be collected before the Gpu* objects.
    //          Static fields act as GC Roots, ensuring the API wrappers remain alive as long as the process runs.
    private static readonly WebGPU      WgpuStatic      = WebGPU.GetApi();
    private static readonly Wgpu        WgpuExStatic    = GetDeviceExtension();
    
    private static Wgpu GetDeviceExtension() {
        if (!WgpuStatic.TryGetDeviceExtension(null, out Wgpu wgpuEx)) {
            throw new Exception("WGPU extension not found!");
        }
        return wgpuEx;
    }
    
    private WgpuInstance(WebGPU wgpu, Wgpu wgpuEx, Instance* instance)
    {
        this.wgpu       = wgpu;
        this.wgpuEx     = wgpuEx;
        this.instance   = instance;
    }
    
    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~WgpuInstance() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool disposing)
    {
        if (isDisposed) return;
        if (instance != null) {
            wgpu.InstanceRelease(instance);
        }        
        isDisposed = true;
    }

    public static WgpuInstance CreateInstance(InstanceExtras instanceExtras)
    {
        var wgpu    = WgpuStatic;
        var wgpuEx  = WgpuExStatic;
        var extras  = instanceExtras;
        
        const SType wgpuSTypeInstanceExtras = (SType)0x60000001;
        extras.Chain = new ChainedStruct {
            SType = wgpuSTypeInstanceExtras
        };
        extras.Chain.Next = null;
        
		var instDesc = new InstanceDescriptor {
            NextInChain = (ChainedStruct*)&extras
        };
		var instance = wgpu.CreateInstance(&instDesc);
        if (instance == null) {
            throw new Exception("The Void Stares Back: Failed to create GpuInstance. Check your drivers!");
        }
        return new WgpuInstance(wgpu, wgpuEx, instance);
    }
    
    public WgpuAdapter RequestAdapter(RequestAdapterOptions options, WgpuAdapterInfo adapterInfo)
    {
		Adapter* adapter = null;
        if (adapterInfo != null) {
            adapter = adapterInfo.Adapter;
        } else {
		    wgpu.InstanceRequestAdapter(instance, &options, PfnRequestAdapterCallback.From((status, adp, _, _) => {
			    if (status == RequestAdapterStatus.Success) adapter = adp;
		    }), null);
        }
        var startTime = Stopwatch.StartNew();
        var timeOutMs = 1000;
        while (adapter == null) {
            PumpEvents(wgpu, wgpuEx, instance);
            if (startTime.ElapsedMilliseconds > timeOutMs) throw new TimeoutException("While requesting adapter");
        }
        if (adapter == null) {
            Console.WriteLine("Adapter-Timeout: driver was found. but no callback was fired");
        }
        return new WgpuAdapter(wgpu, wgpuEx, adapter, instance);
    }
    
    public GlobalReport GenerateReport () {
        var report = new GlobalReport();
        wgpuEx.GenerateReport(instance, ref report);
        return report;
    }
    
    internal static void PumpEvents(WebGPU wgpu, Wgpu wgpuEx, Instance* instance)
    {
        wgpu.InstanceProcessEvents(instance);

        // This check is required when running on Linux using only Software GPU
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            var enumOptions = new InstanceEnumerateAdapterOptions();
            Adapter* dummyAdapter = null;
            // Trigger processing pending callbacks
            wgpuEx.InstanceEnumerateAdapters(instance, &enumOptions, ref dummyAdapter);
            Thread.Yield(); // enable other threads on Linux processing events 
        }
    }
    
    public WgpuAdapterInfo[] GetAdapterInfos()
    {
        InstanceEnumerateAdapterOptions options = default;
        nuint adapterCount = wgpuEx.InstanceEnumerateAdapters(instance, &options, null);
        var infos = new WgpuAdapterInfo[adapterCount];
        
        Adapter** adapters = stackalloc Adapter*[ (int)adapterCount ];
        wgpuEx.InstanceEnumerateAdapters(instance, &options, adapters);
        for (int i = 0; i < (int)adapterCount; i++)
        {
            Adapter* adapter = adapters[i];
            AdapterProperties props = default;
            wgpu.AdapterGetProperties(adapter, &props);
            var name    = WgpuAdapterInfo.PtrToString(props.Name);
            var driver  = WgpuAdapterInfo.PtrToString(props.Name);
            infos[i]    = new WgpuAdapterInfo(props, adapter);
        }
        return infos;
    }
}

