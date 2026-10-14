// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Net;

// ReSharper disable RedundantLambdaParameterType
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;


internal record struct SignalMessage(string type, string? app, string connection, string? sdpOrCandidate)
{
    public override string ToString() => $"[{type}]  app: {app}  conn: {connection}";
}



public partial class SignalingListener
{
    // Maps each WebSocket session to its respective peer session wrapper
    private readonly ConcurrentDictionary<string, PeerSession> _sessions = new();
    
    private static bool TryParse(ReadOnlySpan<byte> buffer, out SignalMessage message)
    {
        try {
            message = JsonSerializer.Deserialize<SignalMessage>(buffer);
            return true;
        }
        catch (JsonException) {
            message = default;
            return false;
        }
    }

    private async Task ProcessSignalingMessage(WebSocket webSocket, ReadOnlyMemory<byte> buffer)
    {
        if (!TryParse(buffer.Span, out var message)) {
            return;
        }

        switch (message.type)
        {
            case "offer":
            {
                // Get or create peer session using a method group (no lambdas)
                var session = _sessions.GetOrAdd(message.connection, new PeerSession(webSocket, message.connection));
                var pc = session.peerConnection;

                // Set remote description using RTCSessionDescriptionInit
                var remoteDesc = new RTCSessionDescriptionInit {
                    sdp = message.sdpOrCandidate,
                    type = RTCSdpType.offer
                };
                SetDescriptionResultEnum result = pc.setRemoteDescription(remoteDesc);

                // Create and set local answer
                var answer = pc.createAnswer(null);
                await pc.setLocalDescription(answer);

                var answerPayload = JsonSerializer.SerializeToUtf8Bytes(
                    new SignalMessage("answer", null, message.connection, answer.sdp)
                );
                if (webSocket.State == WebSocketState.Open) {
                    await webSocket.SendAsync(answerPayload, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                break;
            }
            case "answer":
            {
                if (_sessions.TryGetValue(message.connection, out var session) && message.sdpOrCandidate != null) {
                    var remoteDesc = new RTCSessionDescriptionInit {
                        sdp = message.sdpOrCandidate,
                        type = RTCSdpType.answer
                    };
                    session.peerConnection.setRemoteDescription(remoteDesc);
                }
                break;
            }
            case "candidate":
            {
                if (_sessions.TryGetValue(message.connection, out var session) && !string.IsNullOrEmpty(message.sdpOrCandidate)) {
                    // Deserialize ICE candidate using System.Text.Json
                    var candidateInit = JsonSerializer.Deserialize<RTCIceCandidateInit>(message.sdpOrCandidate);
                    if (candidateInit != null) {
                        session.peerConnection.addIceCandidate(candidateInit);
                    }
                }
                break;
            }
        }
    }
}

internal class PeerSession
{
    private  readonly   WebSocket           webSocket;
    private  readonly   string              connection;
    internal readonly   RTCPeerConnection   peerConnection;

    internal PeerSession(WebSocket webSocket, string connection)
    {
        this.webSocket  = webSocket;
        this.connection = connection;
        peerConnection  = new RTCPeerConnection(null);
        peerConnection.onicecandidate += OnIceCandidate;
        
        peerConnection.ondatachannel += (RTCDataChannel rtcDataChannel) => {
            Console.WriteLine("P2P Data Channel opened on App side!");
            rtcDataChannel.onmessage += (RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data) =>
            {
                var message = System.Text.Encoding.UTF8.GetString(data);
                Console.WriteLine($"Received from client: {message}");
            }; 
        };
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