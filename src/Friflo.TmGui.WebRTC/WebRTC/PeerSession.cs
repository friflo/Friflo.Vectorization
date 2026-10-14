// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Net;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;


internal sealed class PeerSession
{
    private  readonly   WebSocket           webSocket;
    private  readonly   string              connection;
    internal readonly   RTCPeerConnection   peerConnection;

    internal PeerSession(WebSocket webSocket, string connection)
    {
        this.webSocket  = webSocket;
        this.connection = connection;
        peerConnection  = new RTCPeerConnection(null);
        peerConnection.onicecandidate   += OnIceCandidate;
        peerConnection.ondatachannel    += OnDataChannel;
    }
    
    private void OnDataChannel(RTCDataChannel rtcDataChannel)
    {
        Console.WriteLine("P2P Data Channel opened on App side!");
        rtcDataChannel.onmessage += OnMessage; 
    }
    
    private void OnMessage (RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        var message = System.Text.Encoding.UTF8.GetString(data);
        Console.WriteLine($"Received from client: {message}");
    }

    // Returns void to match the event delegate signature and fix CS0407
    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        _ = SendIceCandidateAsync(candidate);
    }

    private async Task SendIceCandidateAsync(RTCIceCandidate candidate)
    {
        var candidateJson = JsonSerializer.Serialize(candidate);
        var msg              = new SignalMessage("candidate", null, connection, candidateJson);
        var candidatePayload = JsonSerializer.SerializeToUtf8Bytes(msg);
        if (webSocket.State == WebSocketState.Open) {
            await webSocket.SendAsync(candidatePayload, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
