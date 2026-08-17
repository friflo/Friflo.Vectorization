// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Friflo.WGPU.Runtime.WebGPU_native;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.Runtime;

[EditorBrowsable(EditorBrowsableState.Never)]
internal readonly unsafe struct WgpuQueue
{
    internal readonly   Queue*  handle;
    
    internal WgpuQueue(Queue* handle) {
        this.handle = handle;
    }
    
    // TODO use this static method to avoid allocation by lambda
    private static void GlobalWorkDoneCallback(QueueWorkDoneStatus status, void* userData)
    {
        // Cast userData pointer back to GCHandle
        GCHandle handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is CommandRecorder recorder) {
            handle.Free(); // free handle - otherwise leak
        }
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void QueueWorkDone_callback(QueueWorkDoneStatus status, StringView message, void* userData1, void* userData2)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData1);
        if (handle.Target is Action<QueueWorkDoneStatus> callback) {
            callback(status);
        }
        handle.Free();
    }

    internal void OnSubmittedWorkDone(int i, Action<QueueWorkDoneStatus> callback)
    {
        // Pin callback to avoid moving callback by GC. Handle is freed in QueueWorkDone_callback()
        GCHandle callbackHandle = GCHandle.Alloc(callback); 
        var callbackInfo = new QueueWorkDoneCallbackInfo {
            mode        = CallbackMode.AllowProcessEvents, 
            callback    = &QueueWorkDone_callback,
            userdata1   = (void*)GCHandle.ToIntPtr(callbackHandle)
        };
        wgpuQueueOnSubmittedWorkDone(handle, callbackInfo);
    }
}