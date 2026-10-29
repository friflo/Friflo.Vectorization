// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui.Session;
using SIPSorcery.Net;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class WebRtcClient : SmClient
{
    private             RTCDataChannel  dataChannel;
    private             WsDoubleBuffer? doubleBuffer;
    
    private readonly    MemoryStream    outputStream        = new();
    private readonly    DeflateStream   statefulCompressor;

    internal WebRtcClient(RTCDataChannel dataChannel, SmSessionLoop loop)
    : base(loop)
    {
        statefulCompressor  = new DeflateStream(outputStream, CompressionLevel.Fastest, leaveOpen: true);
        doubleBuffer        = new WsDoubleBuffer(this);
        // compressor = new DeflaterOutputStream(outputStream, deflater) { IsStreamOwner = false };
        
        this.dataChannel    = dataChannel;
        dataChannel.ordered = true;
        // dataChannel.maxPacketLifeTime = 1;
        // dataChannel.maxRetransmits = 0;
        dataChannel.onmessage   += OnMessage;
        dataChannel.onclose     += OnClose;
    }
    
    public override void Dispose()
    {
        doubleBuffer?.Dispose();
        doubleBuffer    = null!;
        dataChannel     = null!;
    }
    
    internal void EnqueueConnected()
    {
        EventQueue?.TryEnqueueEvent(LoopEventType.WebsocketConnected, this, default);
    }
    
    public override UiType UiType => UiType.WebRtcGui;

    protected override ValueTask<int> SendAsync(SmSendMessage message, CancellationToken cancellationToken)
    {
        var exception = new InvalidOperationException("currently not required");
        return ValueTask.FromException<int>(exception);
    }
    
    // private static readonly byte[] SyncFlushTail = [0x00, 0x00, 0xFF, 0xFF];
    private readonly byte[] lengthBuffer = new byte[sizeof(int)]; 

    private byte[] CompressMessage(ReadOnlyMemory<byte> rawData, out int length)
    {
        MemoryMarshal.Write(lengthBuffer, rawData.Length);
        outputStream.Position = 0;
        outputStream.SetLength(0);
        outputStream.Write(lengthBuffer);
        
        statefulCompressor.Write(rawData.Span);
        statefulCompressor.Flush();
        
        // outputStream.Write(SyncFlushTail);
        
        length = (int)outputStream.Length;
        
        if (outputStream.TryGetBuffer(out var buffer)) {
            var array = buffer.Array; 
            if (array != null) {
                return array;
            }
        }
        return outputStream.ToArray();
    }
    
    // private byte[] sendBuffer = [];

    
    protected override int Send(SmSendMessage message)
    {
        if (message.IsEmpty) {
            return 0;
        }
        if (doubleBuffer == null || dataChannel.readyState != RTCDataChannelState.open) {
            return 0;
        }
        // var sendBuffer = CompressMessageSharpZipLib(message.buffer, out int length);
        var sendBuffer = CompressMessage(message.buffer, out int length);
        
        /* var length = message.Length;
        if (sendBuffer.Length < length) {
            sendBuffer = new byte[length];
        } 
        message.buffer.CopyTo(new Memory<byte>(sendBuffer)); */

        // RTCDataChannel.send(byte[] data, int offset, int count) does not store a reference to passed data.
        // It creates multiple small byte[1020] arrays to ensure they fit in an MTU (Maximum Transmission Unit, 1200 in SIPSorcery).
        dataChannel.send(sendBuffer, 0, length);
        
        /* doubleBuffer.AppendMessage(message);
        if (dataChannel.bufferedAmount == 0) {
            FlushPendingBuffer();
        } */
        return length;
    }

    protected override void SendCommand(SmSendCommand command) {
        if (dataChannel.readyState != RTCDataChannelState.open) {
            return;
        }
        dataChannel.send(command.payload);
    }

    protected override void RestoreTerminal() { } // only applicable for terminal emulators

    private void FlushPendingBuffer()
    {
        var buffer = doubleBuffer!;
        buffer.Swap();

        if (buffer.PendingLength > 0 && dataChannel.readyState == RTCDataChannelState.open) {
            dataChannel.send(buffer.Data, 0, buffer.PendingLength);

            buffer.Clear();
        }
    }
    
    private void OnClose()
    {
        EventQueue?.TryEnqueueClientEvent(this, LoopEventType.WebsocketDisconnected, default);
        // Console.WriteLine($"RTCDataChannel closed");
    }
    
    private void OnMessage (RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        // var debugMsg = System.Text.Encoding.UTF8.GetString(data);
        // Console.WriteLine($"Received from client: {debugMsg}");
        
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        Array.Copy(data, 0, buffer, 0, data.Length);
        var payload = new Payload(buffer, data.Length);
        
        EventQueue?.TryEnqueueClientEvent(this, LoopEventType.WebsocketInput, payload);
    }
}