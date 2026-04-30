// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.Vectorization.GPU;

internal sealed unsafe class GpuQueue
{
    private readonly    GpuContext  context;
    internal            Queue*      Handle { get; private set; }
    
    public GpuQueue(GpuContext ctx, Queue* handle) {
        context = ctx;
        Handle  = handle;
    }
    
    public void WriteBuffer(Buffer* buffer, uint offsetInBytes, void* data, uint byteSize)
    {
        var ctx = context;
        ctx.wgpu.QueueWriteBuffer(ctx.QueuePtr, buffer, offsetInBytes, data, byteSize);
    }
    
    public void Submit(GpuCommandBuffer commandBuffer)
    {
        var handle  = commandBuffer.handle;
        var ctx     = context;
        ctx.wgpu.QueueSubmit(ctx.QueuePtr, 1, &handle);
    }
    
    // TODO use this static method to avoid allocation by lambda
    private static void GlobalWorkDoneCallback(QueueWorkDoneStatus status, void* userData)
    {
        // Wir casten den userData Pointer zurück auf ein GCHandle
        GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is GpuTask task) {
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
        context.wgpu.QueueOnSubmittedWorkDone(Handle, NativeWorkDoneCallback, userData);
    }
}