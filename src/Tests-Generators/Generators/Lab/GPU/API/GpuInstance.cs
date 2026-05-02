// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Friflo.Vectorization.GPU;

/*
    Important note for Dispose pattern
    ----------------------------------
        
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);  // prevent execution of finalizer while Dispose() is called manually
    }
    
    ~GpuClass() {
        Dispose(false);  // false: release only native pointers. Can be called from any thread.
    }
    
    private void Dispose(bool disposing)
    {
        if (isDisposed) return;
        // Early out with isDisposed and GC.SuppressFinalize(this) ensures this code path is executed only once

        // Other managed objects MUST not be touched if disposing == false.
        if (disposing) {
            // cleanup up managed resources
            ...
        }
        // Release native resources. Order matters
        // Native pointer MUST be checked for null. Their creation may have failed
        if (QueuePtr != null) {
            wgpu.QueueRelease(QueuePtr);
        }
        if (DevicePtr != null) {
            wgpu.DeviceSetUncapturedErrorCallback(DevicePtr, null, null); // release callback before device
            wgpu.DeviceRelease(DevicePtr);
        }
        // Free anchor to managed world MUST be the last call
        if (deviceHandle.IsAllocated) {
            deviceHandle.Free();
        }
        isDisposed = true;
    }

 */


public sealed unsafe class GpuInstance : IDisposable
{
    private readonly    WebGPU      wgpu;
    private readonly    Wgpu        wgpuEx;
    private readonly    Instance*   instance;
    private             bool        isDisposed;
    
    private GpuInstance(WebGPU wgpu, Wgpu wgpuEx, Instance* instance)
    {
        this.wgpu       = wgpu;
        this.wgpuEx     = wgpuEx;
        this.instance   = instance;
    }
    
    public void Dispose() {
        if (isDisposed) return;
        wgpu.InstanceRelease(instance);
        isDisposed = true;
    }

    public static GpuInstance CreateInstance()
    {
        WebGPU wgpu = WebGPU.GetApi();
        if (!wgpu.TryGetDeviceExtension(null, out Wgpu wgpuEx)) {
            throw new Exception("WGPU extension not found!");
        }
		// instance & surface (optional, For computing, the adapter is often sufficient)
		InstanceDescriptor instDesc = new InstanceDescriptor();
		var instance = wgpu.CreateInstance(&instDesc);
        return new GpuInstance(wgpu, wgpuEx, instance);
    }
    
    public GpuAdapter RequestAdapter(RequestAdapterOptions options)
    {
		Adapter* adapter = null;

		wgpu.InstanceRequestAdapter(instance, &options, PfnRequestAdapterCallback.From((status, adp, _, _) => {
			if (status == RequestAdapterStatus.Success) adapter = adp;
		}), null);

        var startTime = Stopwatch.StartNew();
        var timeOutMs = 1000;
        while (adapter == null) {
            PumpEvents(wgpu, wgpuEx, instance);
            if (startTime.ElapsedMilliseconds > timeOutMs) throw new TimeoutException("While requesting adapter");
        }
        if (adapter == null) {
            Console.WriteLine("Adapter-Timeout: driver was found. but no callback was fired");
        }
        return new GpuAdapter(wgpu, wgpuEx, adapter, instance);
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
}

