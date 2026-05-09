// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.WGPU;

[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly unsafe struct WgpuQueue
{
    private  readonly   WgpuDevice   device;
    internal readonly   Queue*      handle;
    
    public WgpuQueue(WgpuDevice device, Queue* handle) {
        this.device = device;
        this.handle = handle;
    }
    
    public void WriteBuffer(Buffer* buffer, uint offsetInBytes, void* data, uint byteSize)
    {
        device.wgpu.QueueWriteBuffer(device.QueuePtr, buffer, offsetInBytes, data, byteSize);
    }
    
    // TODO use this static method to avoid allocation by lambda
    private static void GlobalWorkDoneCallback(QueueWorkDoneStatus status, void* userData)
    {
        // Cast userData pointer back to GCHandle
        GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is WgpuTask task) {
            task.IsCompleted = true;
            handle.Free(); // free handle - otherwise leak
        }
    }
    
    // We keep a static reference to avoid GC is not moving/collection the callback
    private static readonly PfnQueueWorkDoneCallback NativeWorkDoneCallback = 
        PfnQueueWorkDoneCallback.From(HandleNativeWorkDone);

    private static void HandleNativeWorkDone(QueueWorkDoneStatus status, void* userData)
    {
        // userData enables going back to CLR
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is Action<QueueWorkDoneStatus> callback) {
            callback(status);
        }
        handle.Free(); // free to avoid leak
    }

    public void OnSubmittedWorkDone(int i, Action<QueueWorkDoneStatus> callback)
    {
        // We have to pin the callback to avoid moving callback by GC
        GCHandle handle = GCHandle.Alloc(callback); // handle is freed in HandleNativeWorkDone()
        void* userData = (void*)GCHandle.ToIntPtr(handle);

        // call native API with static function pointer
        device.wgpu.QueueOnSubmittedWorkDone(this.handle, NativeWorkDoneCallback, userData);
    }
}