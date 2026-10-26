// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using Friflo.SmGui.Session;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SignalSession
{
    private  readonly   SmSignalingListener listener;
    private  readonly   WebSocket           webSocket;
    private  readonly   string              connection;
    internal readonly   RTCPeerConnection   peerConnection;

    internal SignalSession(SmSignalingListener listener, WebSocket webSocket, string connection)
    {
        this.listener   = listener;
        this.webSocket  = webSocket;
        this.connection = connection;
        peerConnection  = new RTCPeerConnection(null);
        peerConnection.onicecandidate   += OnIceCandidate;
        peerConnection.ondatachannel    += OnDataChannel;
    }
    
    // Returns void to match the event delegate signature and fix CS0407
    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        var candidateJson = JsonSerializer.Serialize(candidate);
        var msg              = new SignalMessage("candidate", null, connection, candidateJson);
        var candidatePayload = JsonSerializer.SerializeToUtf8Bytes(msg);
        if (webSocket.State == WebSocketState.Open) {
            _ = webSocket.SendAsync(candidatePayload, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
    
    private void OnDataChannel(RTCDataChannel dataChannel)
    {
        // Console.WriteLine("P2P Data Channel opened on App side!");
        if (dataChannel.label == "tm-gui-channel") {
            var client = new WebRtcClient(listener.loop, dataChannel);
            
            _ = listener.loop.EnqueueEventAsync(client, LoopEventType.WebsocketConnected, default);
            listener.RemoveSession(connection, webSocket);
            return;
        }
        if (dataChannel.label == "api-channel") {
            _ = new WebRtcApiChannel(listener.loop, dataChannel);
        }
    }
}
