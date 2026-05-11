// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Friflo.Vectorization.GPU;
using Friflo.Vectorization.WebGPU.Runtime;
using static Friflo.Vectorization.WebGPU.Runtime.WebGPU_native;


// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WebGPU;

public sealed unsafe class WgpuAdapter : GpuAdapter
{
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
            wgpuAdapterRelease(adapter);
        }
        isDisposed = true;
    }
    
    internal WgpuAdapter(Adapter* adapter, Instance* instance, WgpuAdapterInfo info)
    {
        this.adapter    = adapter;
        this.instance   = instance;
        this.info       = info;
    }

    public override GpuDevice CreateDevice(string label, int maxTasks = 64, int slotSize = 64 * 1024)
    {
		Device* device = null;
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        var len = WgpuUtils.CopySpanToBuffer(label, labelBuffer, labelMaxCount);

		var devDesc = new DeviceDescriptor {
			label = WgpuUtils.FromPtrLength(labelBuffer, len)
		};

		wgpuAdapterRequestDevice(adapter, &devDesc, PfnRequestDeviceCallback.From((status, dev, _, _) => {
			if (status == RequestDeviceStatus.Success) device = dev;
		}), null);

        var startTime = Stopwatch.StartNew();
        var timeOutMs = 1000;
        while (device == null) {
            WgpuInstance.PumpEvents(instance);
            if (startTime.ElapsedMilliseconds > timeOutMs) throw new TimeoutException("While requesting device");
        }

        // Important: wgpu.QueueRelease() must not be called. Queue* shares the lifetime of Device*
		var queuePtr = wgpuDeviceGetQueue(device);
        
        wgpu.DeviceSetUncapturedErrorCallback(device, GlobalErrorCallback, null);
        
        return new WgpuDevice(label, device, queuePtr, maxTasks, slotSize);
    }
    
    public override GpuLimits GetAdapterLimits()
    {
        var limits = new Limits();
        wgpuAdapterGetLimits(adapter, &limits);
        return new GpuLimits {
            MaxStorageBufferBindingSize         = limits.maxStorageBufferBindingSize,  
            MaxComputeWorkgroupStorageSize      = limits.maxComputeWorkgroupStorageSize, 
            MaxBindGroups                       = limits.maxBindGroups, 
            MaxComputeInvocationsPerWorkgroup   = limits.maxComputeInvocationsPerWorkgroup, 
        };
    }
    
    public override GpuHandleDiff GenerateHandles () {
        var globalReport = new GlobalReport();
        wgpuGenerateReport(instance, &globalReport);
        return GpuHandles(globalReport.hub);
    }
    
    private static GpuHandleDiff GpuHandles(in HubReport report)
    {
        return new GpuHandleDiff {
            Devices             = new GpuHandle((long)report.devices.            numKeptFromUser),
            Buffers             = new GpuHandle((long)report.buffers.            numKeptFromUser),
            BindGroups          = new GpuHandle((long)report.bindGroups.         numKeptFromUser),
            BindGroupLayouts    = new GpuHandle((long)report.bindGroupLayouts.   numKeptFromUser),
            ComputePipelines    = new GpuHandle((long)report.computePipelines.   numKeptFromUser),
            CommandBuffers      = new GpuHandle((long)report.commandBuffers.     numKeptFromUser),
            ShaderModules       = new GpuHandle((long)report.shaderModules.      numKeptFromUser),
            PipelineLayouts     = new GpuHandle((long)report.pipelineLayouts.    numKeptFromUser)
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

