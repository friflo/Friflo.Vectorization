// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SignalSession
{
    private  readonly   SmSignalingClient   client;
    private  readonly   WebSocket           webSocket;
    private  readonly   string              connection;
    internal readonly   PeerConnection      peerConnection;    

    internal SignalSession(SmSignalingClient client, WebSocket webSocket, string connection)
    {
        this.client     = client;
        this.webSocket  = webSocket;
        this.connection = connection;
        peerConnection  = RtcFactory.Instance.CreatePeerConnection();
        peerConnection.onicecandidate   += OnIceCandidate;
        peerConnection.ondatachannel    += OnDataChannel;
    }
    
    // Returns void to match the event delegate signature and fix CS0407
    private void OnIceCandidate(IceCandidate candidate)
    {
        var candidateJson    = RtcFactory.Instance.CandidateToJson(candidate);
        var msg              = new SignalMessage("candidate", null, connection, candidateJson);
        var candidatePayload = JsonSerializer.SerializeToUtf8Bytes(msg, ApiJson.Default.SignalMessage);
        if (webSocket.State == WebSocketState.Open) {
            _ = webSocket.SendAsync(candidatePayload, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
    
    private void OnDataChannel(DataChannel dataChannel)
    {
        // Console.WriteLine("P2P Data Channel opened on App side!");
        if (dataChannel.label == "tm-gui-channel") {
            var webRtcClient = new WebRtcClient(dataChannel, client.loop);
            
            webRtcClient.EnqueueConnected();
            client.RemoveSession(connection, webSocket);
            return;
        }
        if (dataChannel.label == "api-channel") {
            _ = new WebRtcChannel(client.loop, dataChannel);
        }
    }
}
