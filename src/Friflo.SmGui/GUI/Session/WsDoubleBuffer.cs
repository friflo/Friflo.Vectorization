// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Threading;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;


internal struct WsSendBuffer
{
    internal    byte[]                  data            = [];
    private     int                     pendingLength;
    
    internal    int                     PendingLength   => pendingLength;
    internal    ReadOnlyMemory<byte>    Memory          => new(data, 0, pendingLength);
    
    internal int GetPendingLength() {
        return Volatile.Read(ref pendingLength);
    }
    
    public override string ToString() => $"byte[{pendingLength}]";
    
    internal void Clear() {
        pendingLength = 0;
    }

    internal int AppendFrom(SmSendMessage message)
    {
        if (message.mode == SmSendMessageMode.DropUnsent) {
            pendingLength = 0;
        }
        int newLength = pendingLength + message.Length;
        if (data.Length < newLength) {
            var allocate = Math.Max(data.Length * 2, newLength);
            // var newBuffer = new byte[allocate];
            var newBuffer = GC.AllocateUninitializedArray<byte>(allocate);
            Array.Copy(data, 0, newBuffer, 0, pendingLength);
            data = newBuffer;
        }
        message.buffer.CopyTo(data.AsMemory(pendingLength));
        return pendingLength = newLength;
    }
    
    public WsSendBuffer() { }
}


public class WsDoubleBuffer : IDisposable
{
    private             SmClient        client;
    private readonly    object          bufferLock = new();
    
    // Lock-free flag for signaling: 0 = no data, 1 = frame pending
    private volatile    bool            hasPendingFrame;
    
    // Double-buffering to prevent race conditions between OnFrame thread and Kestrel flush
    private             WsSendBuffer    frontBuffer = new();
    private             WsSendBuffer    backBuffer  = new();
    
    public WsDoubleBuffer(SmClient client)
    {
        this.client = client;
    }
    
    public void Dispose()
    {
        client      = null!;
        frontBuffer = default;
        backBuffer  = default;
    }

    public void AppendMessage(SmSendMessage message)
    {
        lock (bufferLock)
        {
            var pendingLength = frontBuffer.AppendFrom(message);
            hasPendingFrame = true;
            client.QueueDataAppended(pendingLength);
            // Console.WriteLine(pendingLength);
        }
    }
    
    public int                  PendingLength   => backBuffer.PendingLength;
    public ReadOnlyMemory<byte> Memory          => backBuffer.Memory;
    public byte[]               Data            => backBuffer.data;

    public bool                 HasPendingFrame => hasPendingFrame;
    

    public void Swap()
    {
        // Swap buffers quickly under lock to minimize OnFrame thread wait time
        lock (bufferLock)
        {
            if (frontBuffer.PendingLength == 0) {
                hasPendingFrame = false;
                return;
            }

            // Swap front and back buffer structs
            (frontBuffer, backBuffer) = (backBuffer, frontBuffer);
            
            // Clear pending length on new frontBuffer so it is ready for the next frame
            frontBuffer.Clear();
            hasPendingFrame = false;
        }
    }
    
    public void Clear()
    {
        backBuffer.Clear();
        var queueSize = frontBuffer.GetPendingLength();
        client.QueueDataSent(queueSize);
        // Console.WriteLine(queueSize);
    }
}