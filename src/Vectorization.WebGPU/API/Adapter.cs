// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
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
    
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void UncapturedError_callback(Device** device, ErrorType errorType, StringView message, void* userdata1, void* userdata2) {
        if (userdata1 == null) return;
        var handle = GCHandle.FromIntPtr((IntPtr)userdata1);
        if (handle.Target is WgpuErrorHandler handler) {
            lock (handler) {
                handler.OnGpuError(errorType, message, userdata2);
            }
        }
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void RequestDevice_callback(RequestDeviceStatus status, Device* device, StringView message, void* userdata1, void* userdata2)
    {
        if (userdata1 == null) return;
        var devicePtr = (Device**)userdata1;
        *devicePtr = device;
    }

    public override GpuDevice CreateDevice(string label, int uniformBufferSize = 64 * 1024)
    {
		Device* device = null;
        int     labelMaxCount   = WgpuUtils.GetMaxCount(label);
        byte*   labelBuffer     = stackalloc byte[labelMaxCount];
        var len = WgpuUtils.CopySpanToBuffer(label, labelBuffer, labelMaxCount);
        
        var errorHandler    = new WgpuErrorHandler();
        var errorHandle     = GCHandle.Alloc(errorHandler);
        
		var deviceDesc = new DeviceDescriptor {
			label = WgpuUtils.FromPtrLength(labelBuffer, len),
            uncapturedErrorCallbackInfo = new UncapturedErrorCallbackInfo {
                callback = &UncapturedError_callback,
                userdata1 = (void*)GCHandle.ToIntPtr(errorHandle)
            }
		};
        var callbackInfo = new RequestDeviceCallbackInfo {
            mode        = CallbackMode.WaitAnyOnly, 
            callback    = &RequestDevice_callback,
            userdata1   = &device,
        };
		var future = wgpuAdapterRequestDevice(adapter, &deviceDesc, callbackInfo);
        if (future.id != 0) {
            var waitInfo = new FutureWaitInfo { future = future, completed = 0 };
            wgpuInstanceWaitAny(instance, 1, &waitInfo, 2000);
        }
        if (device == null) {
            throw new Exception("Failed to create WebGPU Device. Status: ");
        }
        /* var startTime = Stopwatch.StartNew();
        var timeOutMs = 1000;
        while (device == null) {
            WgpuInstance.PumpEvents(instance);
            if (startTime.ElapsedMilliseconds > timeOutMs) throw new TimeoutException("While requesting device");
        } */

        // Important: wgpu.QueueRelease() must not be called. Queue* shares the lifetime of Device*
		var queuePtr = wgpuDeviceGetQueue(device);
        
        // wgpu.DeviceSetUncapturedErrorCallback(device, GlobalErrorCallback, null);
        
        return new WgpuDevice(label, errorHandler, errorHandle, instance, device, queuePtr, uniformBufferSize);
    }
    
    public override GpuLimits GetAdapterLimits()
    {
        var limits = new Limits();
        wgpuAdapterGetLimits(adapter, &limits);
        return new GpuLimits {
            MaxStorageBufferBindingSize         = (long)limits.maxStorageBufferBindingSize,  
            MaxComputeWorkgroupStorageSize      = (int) limits.maxComputeWorkgroupStorageSize, 
            MaxBindGroups                       = (int) limits.maxBindGroups, 
            MaxComputeInvocationsPerWorkgroup   = (int) limits.maxComputeInvocationsPerWorkgroup, 
        };
    }
    
    public override GpuHandleDiff GenerateHandles () {
        var globalReport = new GlobalReport();
        wgpuGenerateReport(instance, &globalReport);
        return GpuHandles(globalReport.hub, info);
    }
    
    private static GpuHandleDiff GpuHandles(in HubReport report, GpuAdapterInfo info)
    {
        return new GpuHandleDiff {
            BackendType         = info.BackendType,
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
}

