// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU.Runtime;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;

public sealed unsafe class WgpuAdapter : NativeAdapter
{
    private readonly    WebGPU      wgpu;
    private readonly    Wgpu        wgpuEx;
    private readonly    Adapter*    adapter;
    private readonly    Instance*   instance;
    private             bool        isDisposed;
    public  override    bool        IsDisposed => isDisposed;
    
    public  override    string      ToString() => isDisposed ? "Disposed" : "Alive";
    
    private static readonly PfnErrorCallback GlobalErrorCallback = PfnErrorCallback.From(OnGpuError);
    
    
    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~WgpuAdapter() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool disposing)
    {
        if (isDisposed) return;
        if (adapter != null) {
            wgpu.AdapterRelease(adapter);
        }
        isDisposed = true;
    }
    
    internal WgpuAdapter(WebGPU wgpu, Wgpu wgpuEx, Adapter* adapter, Instance* instance)
    {
        this.wgpu       = wgpu;
        this.wgpuEx     = wgpuEx;
        this.adapter    = adapter;
        this.instance   = instance;
    }
    
    public override GpuDevice CreateDevice(string label, int maxTasks, int slotSize)
    {
		Device* device = null;
        var name = Marshal.StringToHGlobalAnsi(label);
		var devDesc = new DeviceDescriptor {
			Label = (byte*)name
		};

		wgpu.AdapterRequestDevice(adapter, &devDesc, PfnRequestDeviceCallback.From((status, dev, _, _) => {
			if (status == RequestDeviceStatus.Success) device = dev;
		}), null);

        var startTime = Stopwatch.StartNew();
        var timeOutMs = 1000;
        while (device == null) {
            WgpuInstance.PumpEvents(wgpu, wgpuEx, instance);
            if (startTime.ElapsedMilliseconds > timeOutMs) throw new TimeoutException("While requesting device");
        }
        Marshal.FreeHGlobal(name); // after device is set is safe to release. name is consumed asyn

        // Important: wgpu.QueueRelease() must not be called. Queue* shares the lifetime of Device*
		var queuePtr = wgpu.DeviceGetQueue(device);
        
        wgpu.DeviceSetUncapturedErrorCallback(device, GlobalErrorCallback, null);
        
        var native = new WgpuDevice(wgpu, wgpuEx, label, device, queuePtr, maxTasks, slotSize);
        return new GpuDevice(native, label, slotSize);
    }
    
    public override GpuAdapterInfo GetAdapterProperties () {
        var report = new AdapterProperties();
        wgpu.AdapterGetProperties(adapter, ref report);
        var name    = WgpuAdapterInfo.PtrToString(report.Name);
        var driver  = WgpuAdapterInfo.PtrToString(report.DriverDescription);
        return new GpuAdapterInfo(report, name, driver, (IntPtr)adapter);
    }
    
    private static void OnGpuError(ErrorType type, byte* message, void* userData)
    {
        string errorMsg = Marshal.PtrToStringUTF8((IntPtr)message);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine("--- [WEBGPU CRITICAL ERROR] ---");
        Console.Error.WriteLine($"Type: {type}");
        Console.Error.WriteLine($"Message: {errorMsg}");
        Console.Error.WriteLine("-------------------------------");
        Console.ResetColor();
        if (Debugger.IsAttached) Debugger.Break();
    }
}

