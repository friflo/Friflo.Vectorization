// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal sealed class SignalSession
{
    private  readonly   WebSocket           webSocket;
    internal readonly   string              connection;
    internal readonly   PeerConnection      peerConnection;    

    internal SignalSession(PeerConnection conn, WebSocket webSocket, string connection)
    {
        this.webSocket  = webSocket;
        this.connection = connection;
        peerConnection  = conn;
        peerConnection.onicecandidate   += OnIceCandidate;
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
}
