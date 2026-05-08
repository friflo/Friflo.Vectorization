// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Friflo.Vectorization.GPU.Runtime;
using Silk.NET.WebGPU;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.Vectorization.GPU;


public sealed class GpuBuffer<T> : IDisposable where T : unmanaged
{
    public	readonly    NativeBuffer<T> native;
    private readonly    string          label;
    public  readonly    int             Length;
    public	readonly    long            Id;
    public	            GpuDevice       device { get; private set; }
    private             uint            SizeInBytes;
    public              NativeTask      LastWritingTask;
        
    public              bool            IsDisposed => native.IsDisposed;
    
    public  override    string          ToString() => native.ToString();


    public void Dispose() {
        native.Dispose();
        device = null;
    }

    public GpuBuffer(GpuDevice device, uint sizeInBytes, BufferUsage usage, string label) 
    {
        this.device         = device;       // TODO add GpuDevice.CreateBuffer();
        this.SizeInBytes    = sizeInBytes;
        this.label          = label;
        Id                  = GpuBufferUtils.NextId();
        native = new WgpuBuffer<T>((WgpuDevice)device.native, sizeInBytes, usage, label, Id);
    }
    
    public GpuBuffer(GpuDevice device, T[] data, BufferUsage usage, string label) {
        this.device         = device;       // TODO add GpuDevice.CreateBuffer();
        this.label          = label;
        Length              = data.Length;
        Id                  = GpuBufferUtils.NextId();
        native = new WgpuBuffer<T>((WgpuDevice)device.native, data, usage, label, Id);
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                device.Wait(this); // force Compute before CPU reads value
            }
            throw new NotImplementedException();
            // return InternalDownloadValue(index);
        }
    }

    public void WaitInDebug()
    {
        if (!device.DebugMode) {
            return;
        }
        device.Flush();
    }
    
    public void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        native.Download(this, targetArray);
    }
}


