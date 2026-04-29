// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using Silk.NET.WebGPU;
using Buffer = Silk.NET.WebGPU.Buffer;


// ReSharper disable InconsistentNaming
namespace Friflo.Vectorization.GPU;

public unsafe class GpuBuffer<T> : IDisposable where T : unmanaged
{
    internal            Buffer*     _handle { get; private set; }
    private  readonly   GpuContext  _context;
    public              int         Length;
    private             uint        SizeInBytes;
    public              GpuTask     LastWritingTask;


    public  GpuContext Context => _handle != null ? _context :
        throw new InvalidOperationException("Architectural Blasphemy: You are trying to extract the Context from a Buffer that you've already sent to the void. A disposed Buffer has no God.");

    public GpuBuffer(GpuContext ctx, uint sizeInBytes, BufferUsage usage) 
    {
        _context = ctx;
        // Wir speichern die Größe in Bytes, falls wir später Alignment-Checks brauchen
        SizeInBytes = sizeInBytes; 
        
        // Wir berechnen die Länge basierend auf dem Typ T (z.B. float = 4 Bytes)
        Length = (int)(sizeInBytes / sizeof(T));

        // Den Pointer von der API holen
        _handle = ctx.CreateBuffer(sizeInBytes, usage);
    }
    
    public unsafe GpuBuffer(GpuContext ctx, T[] data, BufferUsage usage) 
    {
        _context = ctx;
        Length  = data.Length;
        _handle  = ctx.CreateBufferWithData(data, usage);
    }
    
    public void Dispose()
    {
        if (_handle != null) {
            _context._wgpu.BufferRelease(_handle);
            _handle = null;
        }
    }
    
    public T this[int index]
    {
        get {
            if (LastWritingTask != null && !LastWritingTask.IsCompleted) {
                _context.Wait(this); // force Compute before CPU reads value
            }
            return InternalDownloadValue(index);
        }
    }

    private T InternalDownloadValue(int index)
    {
        throw new NotImplementedException();
    }

    public void WaitInDebug()
    {
        if (!_context.DebugMode) {
            return;
        }
        _context.Flush();
    }
    
    public void Download(GpuBuffer<T> gpuBuffer, T[] targetArray) // TODO  optimize DeviceCreateBuffer und DeviceCreateCommandEncoder are heavy operations
    {
        _context.Flush();
        
        if (targetArray.Length < gpuBuffer.Length)
            throw new Exception("Target array is too small!");

        uint size = (uint)(gpuBuffer.Length * sizeof(T));
        
        // 1. Staging Buffer erstellen (wie zuvor)
        var readDesc = new BufferDescriptor
        {
            Size = size,
            Usage = BufferUsage.CopyDst | BufferUsage.MapRead,
            MappedAtCreation = false
        };
        var ctx         = gpuBuffer._context;
        var _wgpu       = ctx._wgpu;
        var DevicePtr   = ctx.DevicePtr;
        var QueuePtr    = ctx.QueuePtr;
        var readBuffer  = _wgpu.DeviceCreateBuffer(DevicePtr, &readDesc);

        // 2. GPU-interne Kopie
        var encoder = _wgpu.DeviceCreateCommandEncoder(DevicePtr, null);
        _wgpu.CommandEncoderCopyBufferToBuffer(encoder, gpuBuffer._handle, 0, readBuffer, 0, size);
        
        var commandBuffer = _wgpu.CommandEncoderFinish(encoder, null);
        _wgpu.QueueSubmit(QueuePtr, 1, &commandBuffer);

        // 3. Asynchrones Mapping
        bool mapFinished = false;
        var callback = PfnBufferMapCallback.From((status, data) => { mapFinished = true; });
        _wgpu.BufferMapAsync(readBuffer, MapMode.Read, 0, size, callback, null);

        while (!mapFinished) {
            ctx.Poll(true);
        }

        // 4. In das ORIGINAL-Array zurückkopieren
        void* pMapped = _wgpu.BufferGetMappedRange(readBuffer, 0, size);
        fixed (void* pTarget = targetArray) {
            System.Buffer.MemoryCopy(pMapped, pTarget, size, size);
        }
        // 5. Cleanup
        _wgpu.BufferUnmap(readBuffer);
        _wgpu.BufferDestroy(readBuffer);
    }
}

