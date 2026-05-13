// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Silk.NET.WebGPU;
using Silk.NET.WebGPU.Extensions.WGPU;
using Webgpu = Silk.NET.WebGPU.WebGPU;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.SilkWebGPU;

public sealed unsafe class WgpuAdapter : GpuAdapter
{
    private readonly    Webgpu          wgpu;
    private readonly    Wgpu            wgpuEx;
    private readonly    Adapter*        adapter;
    private readonly    Instance*       instance;
    private readonly    WgpuAdapterInfo info;
    private             bool            isDisposed;
        
    public  override    bool            IsDisposed          => isDisposed;
    public  override    GpuAdapterInfo  GetAdapterInfo()    => info;
    
    public  override    string          ToString() => isDisposed ? "Disposed" : "Alive";
    
    private static readonly PfnErrorCallback GlobalErrorCallback = PfnErrorCallback.From(OnGpuError);
    
    
    // Every class implementing IDispose must follow the same pattern. Set GpuInstance code sample.
    public override void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
   ~WgpuAdapter() {
        Dispose(false);  // false: release only native pointers.
    }
    
    private void Dispose(bool _)
    {
        if (isDisposed) return;
        if (adapter != null) {
            wgpu.AdapterRelease(adapter);
        }
        isDisposed = true;
    }
    
    internal WgpuAdapter(Webgpu wgpu, Wgpu wgpuEx, Adapter* adapter, Instance* instance, WgpuAdapterInfo info)
    {
        this.wgpu       = wgpu;
        this.wgpuEx     = wgpuEx;
        this.adapter    = adapter;
        this.instance   = instance;
        this.info       = info;
    }

    public override GpuDevice CreateDevice(string label, int maxTasks = 64, int slotSize = 64 * 1024)
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
        
        return new WgpuDevice(wgpu, wgpuEx, label, device, queuePtr, maxTasks, slotSize);
    }
    
    public override GpuLimits GetAdapterLimits()
    {
        var supportedLimits = new SupportedLimits();
        wgpu.AdapterGetLimits(adapter, &supportedLimits);
        var limits = supportedLimits.Limits;
        return new GpuLimits {
            MaxStorageBufferBindingSize         = limits.MaxStorageBufferBindingSize,  
            MaxComputeWorkgroupStorageSize      = limits.MaxComputeWorkgroupStorageSize, 
            MaxBindGroups                       = limits.MaxBindGroups, 
            MaxComputeInvocationsPerWorkgroup   = limits.MaxComputeInvocationsPerWorkgroup, 
        };
    }
    
    public override GpuHandleDiff GenerateHandles () {
        var globalReport = new GlobalReport();
        wgpuEx.GenerateReport(instance, &globalReport);
        var hubReport = GetReport(globalReport, (BackendType)info.BackendType);
        return GpuHandles(hubReport, info);
    }
    
    private static HubReport GetReport(GlobalReport report, BackendType type)
    {
        return type switch {
            BackendType.Vulkan   => report.Vulkan,
            BackendType.Metal    => report.Metal,
            BackendType.D3D11    => report.Dx12,
            BackendType.D3D12    => report.Dx12,
            _                    => report.Gl,
        };
    }
    
    private static GpuHandleDiff GpuHandles(in HubReport report, GpuAdapterInfo info)
    {
        return new GpuHandleDiff {
            BackendType         = info.BackendType,
            Devices             = new GpuHandle((long)report.Devices.            NumKeptFromUser),
            Buffers             = new GpuHandle((long)report.Buffers.            NumKeptFromUser),
            BindGroups          = new GpuHandle((long)report.BindGroups.         NumKeptFromUser),
            BindGroupLayouts    = new GpuHandle((long)report.BindGroupLayouts.   NumKeptFromUser),
            ComputePipelines    = new GpuHandle((long)report.ComputePipelines.   NumKeptFromUser),
            CommandBuffers      = new GpuHandle((long)report.CommandBuffers.     NumKeptFromUser),
            ShaderModules       = new GpuHandle((long)report.ShaderModules.      NumKeptFromUser),
            PipelineLayouts     = new GpuHandle((long)report.PipelineLayouts.    NumKeptFromUser)
        };
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

