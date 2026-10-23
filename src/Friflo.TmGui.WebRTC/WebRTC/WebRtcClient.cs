// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;
using SIPSorcery.Net;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;


internal sealed class WebRtcClient : TmClient
{
    private     RTCDataChannel  dataChannel;
    private     TmSessionLoop   loop;
    private     WsDoubleBuffer? doubleBuffer;

    internal WebRtcClient(TmSessionLoop loop, RTCDataChannel dataChannel)
    {
        doubleBuffer        = new WsDoubleBuffer(this);
        this.loop           = loop;
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
        loop            = null!;
        doubleBuffer    = null!;
        dataChannel     = null!;
    }
    
    public override TmClientType ClientType => TmClientType.WebRtcGui;

    protected override ValueTask<int> SendAsync(TmSendMessage message, CancellationToken cancellationToken)
    {
        var exception = new InvalidOperationException("currently not required");
        return ValueTask.FromException<int>(exception);
    }
    
    private readonly MemoryStream outputGZip = new(1000);
        
    private byte[] CompressMessage(ReadOnlySpan<byte> rawData, out int length)
    {
        outputGZip.Position = 0;
        outputGZip.SetLength(0);
        
        {
            using var gzipStream = new GZipStream(outputGZip, CompressionLevel.Fastest, leaveOpen: true);
            gzipStream.Write(rawData);
        }
        length = (int)outputGZip.Length;
        
        if (outputGZip.TryGetBuffer(out var buffer)) {
            var array = buffer.Array; 
            if (array != null) {
                return array;
            }
        }
        return outputGZip.ToArray();
    }
    
    // private byte[] sendBuffer = [];

    
    protected override int Send(TmSendMessage message)
    {
        if (message.IsEmpty) {
            return 0;
        }
        if (doubleBuffer == null || dataChannel.readyState != RTCDataChannelState.open) {
            return 0;
        }
        var messageSpan = message.buffer.Span;
        var sendBuffer = CompressMessage(messageSpan, out int  length);
        
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

    protected override void SendCommand(TmSendCommand command) {
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
        _ = loop.EnqueueEventAsync(this, ClientEventType.WebsocketDisconnected, default);
        // Console.WriteLine($"RTCDataChannel closed");
    }
    
    private void OnMessage (RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        // var debugMsg = System.Text.Encoding.UTF8.GetString(data);
        // Console.WriteLine($"Received from client: {debugMsg}");
        
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        Array.Copy(data, 0, buffer, 0, data.Length);
        var payload = new Payload(buffer, data.Length);
        
        _ = loop.EnqueueEventAsync(this, ClientEventType.WebsocketInput, payload);
    }
}