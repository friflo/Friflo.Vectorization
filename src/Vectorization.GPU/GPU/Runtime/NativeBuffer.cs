// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU._Native;

public abstract class NativeBuffer<T> : IDisposable where T : unmanaged
{
//  private readonly    string      label;
//  public  readonly    int         Length;
//  public  readonly    long        Id;
//  private             uint        SizeInBytes;
    public              NativeTask  LastWritingTask { get; internal set; }
    public  abstract    bool        IsDisposed { get; }
    

    public abstract void Dispose();
    
    public abstract void Download(GpuBuffer<T> gpuBuffer, T[] targetArray);
}
