// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui.RTC;


// ReSharper disable SuggestVarOrType_SimpleTypes
// ReSharper disable RedundantLambdaParameterType
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.WebRTC;


internal record struct SignalMessage(string type, string? app, string connection, string? sdpOrCandidate, int? sessions = null)
{
    public override string ToString() => $"[{type}]  app: {app}  conn: {connection}";
    
    internal static bool TryParse(ReadOnlySpan<byte> buffer, out SignalMessage message)
    {
        try {
            message = JsonSerializer.Deserialize(buffer, ApiJson.Default.SignalMessage);
            return true;
        }
        catch (JsonException) {
            message = default;
            return false;
        }
    }
}



public sealed partial class SmSignalingClient
{
    // Maps each WebSocket session to its respective peer session wrapper
    private readonly ConcurrentDictionary<string, SignalSession> sessions = new();
    
    private void RemoveSession(string connection, WebSocket webSocket)
    {
        if (!sessions.TryRemove(connection, out _)) {
            return;
        }
        var msg     = new SignalMessage("close", null, connection, null, loop.SessionCount);
        var payload = JsonSerializer.SerializeToUtf8Bytes(msg, ApiJson.Default.SignalMessage);
        if (webSocket.State == WebSocketState.Open) {
            _ = webSocket.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private async Task ProcessSignalingMessage(WebSocket webSocket, ReadOnlyMemory<byte> messageBuffer)
    {
        if (!SignalMessage.TryParse(messageBuffer.Span, out var message)) {
            return;
        }

        switch (message.type)
        {
            case "offer":
            {
                // Get or create peer session using a method group (no lambdas)
                var connection      = RtcFactory.Instance.CreatePeerConnection();
                var guiDataChannel  = await connection.createDataChannel("tm-gui-channel", 1);
                var apiChannel      = await connection.createDataChannel("api-channel",    2);
                
                apiChannel.onopen += () => {
                    _ = new WebRtcChannel(loop, apiChannel);
                };
                
                var signalSession   = new SignalSession(connection, webSocket, message.connection);
                guiDataChannel.onopen += () => {
                    var webRtcClient = new WebRtcClient(guiDataChannel, loop);
                    webRtcClient.EnqueueConnected();
                    RemoveSession(signalSession.connection, webSocket);
                };
                var session = sessions.GetOrAdd(message.connection, signalSession);
                var pc = session.peerConnection;

                // Set remote description using RTCSessionDescriptionInit
                var remoteDesc = RtcFactory.Instance.CreateSessionDescriptionInit(
                    sdp:    message.sdpOrCandidate,
                    type:   SdpType.offer
                );
                await pc.setRemoteDescription(remoteDesc);

                // Create and set local answer
                var answer = await pc.createAnswer(); // createAnswer(null)
                await pc.setLocalDescription(answer);

                var msg = new SignalMessage("answer", null, message.connection, answer.sdp, loop.SessionCount);
                var answerPayload = JsonSerializer.SerializeToUtf8Bytes(msg, ApiJson.Default.SignalMessage);
                if (webSocket.State == WebSocketState.Open) {
                    await webSocket.SendAsync(answerPayload, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                break;
            }
            case "answer":
            {
                if (sessions.TryGetValue(message.connection, out var session) && message.sdpOrCandidate != null) {
                    var remoteDesc = RtcFactory.Instance.CreateSessionDescriptionInit(
                        sdp:    message.sdpOrCandidate,
                        type:   SdpType.answer
                    );
                    await session.peerConnection.setRemoteDescription(remoteDesc);
                }
                break;
            }
            case "candidate":
            {
                if (sessions.TryGetValue(message.connection, out var session) && !string.IsNullOrEmpty(message.sdpOrCandidate)) {
                    // Deserialize ICE candidate using System.Text.Json
                    var candidateInit = RtcFactory.Instance.JsonToCandidate(message.sdpOrCandidate);
                    if (candidateInit.native != null) {
                        await session.peerConnection.addIceCandidate(candidateInit);
                    }
                }
                break;
            }
        }
    }
}
