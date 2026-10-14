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
    
    internal static bool TryParse(ReadOnlySpan<byte> buffer, out SignalMessage message)
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
}



public sealed partial class TmSignalingListener
{
    // Maps each WebSocket session to its respective peer session wrapper
    private readonly ConcurrentDictionary<string, PeerSession> _sessions = new();

    private async Task ProcessSignalingMessage(WebSocket webSocket, ReadOnlyMemory<byte> buffer)
    {
        if (!SignalMessage.TryParse(buffer.Span, out var message)) {
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
