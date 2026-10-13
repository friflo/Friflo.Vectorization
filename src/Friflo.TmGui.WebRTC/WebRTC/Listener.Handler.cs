// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SIPSorcery.Net;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;


internal record SignalingMessage(string type, string? sdpOrCandidate, string? host)
{
    public override string ToString() => $"{type} > host: {host}";
}



public partial class SignalingListener
{
    // Maps each WebSocket session to its respective peer session wrapper
    private readonly ConcurrentDictionary<WebSocket, PeerSession> _sessions = new();
    
    private static SignalingMessage? Parse(ReadOnlySpan<byte> buffer)
    {
        try {
            return JsonSerializer.Deserialize<SignalingMessage>(buffer);
        }
        catch (JsonException) {
            return null;
        }
    }

    private async Task ProcessSignalingMessage(WebSocket webSocket, ReadOnlyMemory<byte> buffer)
    {
        var message = Parse(buffer.Span);
        if (message == null) {
            return;
        }

        switch (message.type)
        {
            case "offer":
            {
                // Get or create peer session using a method group (no lambdas)
                var session = _sessions.GetOrAdd(webSocket, CreatePeerSession);
                var pc = session.peerConnection;

                // Set remote description using RTCSessionDescriptionInit
                var remoteDesc = new RTCSessionDescriptionInit {
                    sdp = message.sdpOrCandidate,
                    type = RTCSdpType.offer
                };
                pc.setRemoteDescription(remoteDesc);

                // Create and set local answer
                var answer = pc.createAnswer(null);
                await pc.setLocalDescription(answer);

                var answerPayload = JsonSerializer.SerializeToUtf8Bytes(
                    new SignalingMessage("answer", answer.sdp, null)
                );
                if (webSocket.State == WebSocketState.Open) {
                    await webSocket.SendAsync(answerPayload, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                break;
            }
            case "answer":
            {
                if (_sessions.TryGetValue(webSocket, out var session) && message.sdpOrCandidate != null) {
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
                if (_sessions.TryGetValue(webSocket, out var session) && !string.IsNullOrEmpty(message.sdpOrCandidate)) {
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

    private static PeerSession CreatePeerSession(WebSocket webSocket)
    {
        return new PeerSession(webSocket);
    }
}

internal class PeerSession
{
    private  readonly   WebSocket            webSocket;
    internal readonly   RTCPeerConnection    peerConnection;

    public PeerSession(WebSocket webSocket)
    {
        this.webSocket = webSocket;
        peerConnection = new RTCPeerConnection(null);
        peerConnection.onicecandidate += OnIceCandidate;
    }

    // Returns void to match the event delegate signature and fix CS0407
    private void OnIceCandidate(RTCIceCandidate candidate)
    {
        _ = SendIceCandidateAsync(webSocket, candidate);
    }

    private static async Task SendIceCandidateAsync(WebSocket webSocket, RTCIceCandidate candidate)
    {
        var candidateJson = JsonSerializer.Serialize(candidate);
        var candidatePayload = JsonSerializer.SerializeToUtf8Bytes(
            new SignalingMessage("candidate", candidateJson, null)
        );
        if (webSocket.State == WebSocketState.Open) {
            await webSocket.SendAsync(candidatePayload, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}