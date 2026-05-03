// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Friflo.Vectorization.GPU;

public sealed unsafe class GpuAdapter : IDisposable
{
    private readonly    WebGPU      wgpu;
    private readonly    Wgpu        wgpuEx;
    private readonly    Adapter*    adapter;
    private readonly    Instance*   instance;
    private             bool        isDisposed;
    public              bool        IsDisposed => isDisposed;
    
    public  override    string      ToString() => isDisposed ? "Disposed" : "Alive";
    
    private static readonly PfnErrorCallback GlobalErrorCallback = PfnErrorCallback.From(OnGpuError);
    
    
    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~GpuAdapter() {
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
    
    internal GpuAdapter(WebGPU wgpu, Wgpu wgpuEx, Adapter* adapter, Instance* instance)
    {
        this.wgpu       = wgpu;
        this.wgpuEx     = wgpuEx;
        this.adapter    = adapter;
        this.instance   = instance;
    }
    
    public GpuDevice CreateDevice(string label, int maxTasks = 64, int slotSize = 64 * 1024)
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
            GpuInstance.PumpEvents(wgpu, wgpuEx, instance);
            if (startTime.ElapsedMilliseconds > timeOutMs) throw new TimeoutException("While requesting device");
        }
        Marshal.FreeHGlobal(name); // after device is set is safe to release. name is consumed asyn

        // Important: wgpu.QueueRelease() must not be called. Queue* shares the lifetime of Device*
		var queuePtr = wgpu.DeviceGetQueue(device);
        
        wgpu.DeviceSetUncapturedErrorCallback(device, GlobalErrorCallback, null);
        
        return new GpuDevice(wgpu, wgpuEx, label, device, queuePtr, maxTasks, slotSize);
    }
    
    public AdapterProperties GetAdapterProperties () {
        var report = new AdapterProperties();
        wgpu.AdapterGetProperties(adapter, ref report);
        return report;
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

