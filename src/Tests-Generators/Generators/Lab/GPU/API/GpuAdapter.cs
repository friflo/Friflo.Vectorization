// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;

namespace Friflo.Vectorization.GPU;

public readonly unsafe struct  GpuAdapter : IDisposable
{
    private readonly    WebGPU      wgpu;
    private readonly    Wgpu        wgpuEx;
    private readonly    Adapter*    adapter;
    private readonly    Instance*   instance;
    
    internal GpuAdapter(WebGPU wgpu, Wgpu wgpuEx, Adapter* adapter, Instance* instance)
    {
        this.wgpu       = wgpu;
        this.wgpuEx     = wgpuEx;
        this.adapter    = adapter;
        this.instance   = instance;
    }
    
    public void Dispose() {
        if (adapter != null) wgpu.AdapterRelease(adapter);
    }
    
    public GpuDevice CreateDevice(int maxTasks = 64, int slotSize = 64 * 1024)
    {
		// 3. Device anfordern
		Device* device = null;
        var name = Marshal.StringToHGlobalAnsi("GpuDevice");
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

		var queuePtr = wgpu.DeviceGetQueue(device);
        
        var errorCallback = PfnErrorCallback.From(OnGpuError);
        wgpu.DeviceSetUncapturedErrorCallback(device, errorCallback, null);
        
        return new GpuDevice(wgpu, wgpuEx, device, queuePtr, errorCallback, maxTasks, slotSize);
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

