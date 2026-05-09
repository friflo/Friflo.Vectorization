// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU.Runtime;

public abstract class NativeDevice : IDisposable
{
    public  abstract    bool    IsDisposed { get; }

    public abstract void Dispose();
    
    public abstract NativeBuffer<T> CreateBuffer<T>(int length, GpuBufferUsage usage, string bufferLabel, long id) where T : unmanaged;
    public abstract NativeBuffer<T> CreateBuffer<T>(T[] data,   GpuBufferUsage usage, string bufferLabel, long id) where T : unmanaged;
    
    // -------------------------------- Task Dependency Tracking --------------------------------
    public abstract void    Flush(bool wait = true);
    public abstract void    Wait<T>(NativeBuffer<T> buffer) where T : unmanaged;
    public abstract void    SubmitGraph(NativeTask finalTask);
}
