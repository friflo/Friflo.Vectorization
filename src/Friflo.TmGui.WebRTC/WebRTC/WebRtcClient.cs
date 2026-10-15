// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;
using SIPSorcery.Net;

// ReSharper disable MergeIntoPattern
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;


internal class WebRtcClient : TmClient
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

    protected override ValueTask<int> SendAsync(TmSendMessage message, CancellationToken cancellationToken)
    {
        var exception = new InvalidOperationException("currently not required");
        return ValueTask.FromException<int>(exception);
    }
    
    protected override int Send(TmSendMessage message)
    {
        if (doubleBuffer == null || dataChannel.readyState == RTCDataChannelState.open) {
            return 0;
        }
        doubleBuffer.AppendMessage(message);
        if (dataChannel.bufferedAmount == 0) {
            FlushPendingBuffer();
        }
        return message.Length;
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
        Console.WriteLine($"RTCDataChannel closed");
    }
    
    private void OnMessage (RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        var debugMsg = System.Text.Encoding.UTF8.GetString(data);
        Console.WriteLine($"Received from client: {debugMsg}");
        
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        Array.Copy(data, 0, buffer, 0, data.Length);
        var payload = new Payload(buffer, data.Length);
        
        _ = loop.EnqueueEventAsync(this, ClientEventType.WebsocketInput, payload);
    }
}