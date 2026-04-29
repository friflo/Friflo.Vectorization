// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

namespace Friflo.Vectorization.GPU;

internal unsafe class GpuQueue
{
    private GpuContext  Context;
    internal Queue*      Handle { get; private set; }
    
    public GpuQueue(GpuContext ctx, Queue* handle) {
        Context = ctx;
        Handle  = handle;
    }
    
    public void WriteBuffer(Buffer* buffer, uint offsetInBytes, void* data, uint byteSize)
    {
        var ctx = Context;
        ctx._wgpu.QueueWriteBuffer(ctx.QueuePtr, buffer, offsetInBytes, data, byteSize);
    }
    
    public void Submit(GpuCommandBuffer commandBuffer)
    {
        var handle = commandBuffer.Handle;
        var ctx = Context;
        ctx._wgpu.QueueSubmit(ctx.QueuePtr, 1, &handle);
    }
    
    // TODO use this static method to avoid allocation by lambda
    private static unsafe void GlobalWorkDoneCallback(QueueWorkDoneStatus status, void* userData)
    {
        // Wir casten den userData Pointer zurück auf ein GCHandle
        GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is GpuTask task) {
            task.IsCompleted = true;
            handle.Free(); // free handle - otherwise leak
        }
    }
    
    // We keep a static reference to avoid GC is not moving/collection the callback
    private static readonly PfnQueueWorkDoneCallback _nativeWorkDoneCallback = 
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
        Context._wgpu.QueueOnSubmittedWorkDone(Handle, _nativeWorkDoneCallback, userData);
    }
}