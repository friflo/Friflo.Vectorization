// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.WebRTC;

public sealed partial class TmSignalingListener
{
    internal readonly TmSessionLoop loop;
        
    public TmSignalingListener(TmSessionLoop loop)
    {
        this.loop = loop;
    }
        
    public Task Start(Uri uri, string app, string? instance = null)
    {
        // Dedicated background thread execution equivalent to TaskCreationOptions.LongRunning
        var serverTask = Task.Factory.StartNew(
            () => Run(uri, app, instance),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        ).Unwrap();
        return serverTask;
    }
        
    private async Task Run(Uri uri, string app, string? instance, CancellationToken cancellationToken = default)
    {
        // Reusable buffer for incoming data chunks
        try
        {
            using var clientWebSocket = new ClientWebSocket();

            // Connect to the specified signaling server URL
            await clientWebSocket.ConnectAsync(uri, cancellationToken);
            
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new SignalMessage{ type = "register", app = app, sdpOrCandidate = instance });

            await clientWebSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);
            
            var result = await ReadChunksAsync(clientWebSocket, cancellationToken);
            
            if (result.MessageType != WebSocketMessageType.Text) {
                return;
            }
            var messageBuffer = memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);
            if (!SignalMessage.TryParse(messageBuffer.Span, out var message)) {
                return;
            }
            if (message.sdpOrCandidate != "registered") {
                return;
            }
            loop.app = message.app;
            
            // Run the receive loop using the connected websocket
            await RunWebSocketReceiveLoopAsync(clientWebSocket, cancellationToken);
        }
        catch (WebSocketException)
        {
            // Handle unexpected drops or client disconnects gracefully
        }
    }
    
    private readonly    MemoryStream    memoryStream = new();
    private readonly    byte[]          buffer = new byte[1024 * 4];
    
    private async Task RunWebSocketReceiveLoopAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await ReadChunksAsync(webSocket, cancellationToken);

            // Process the complete message payload based on its type
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var messageBuffer = memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Length);
                
                // Handle incoming signaling message (e.g., SDP offer/answer or ICE candidate)
                await ProcessSignalingMessage(webSocket, messageBuffer);
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Handle binary payloads if needed
                // byte[] binaryData = memoryStream.ToArray();
            }
        }
    }
    
    private async Task<WebSocketReceiveResult> ReadChunksAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        memoryStream.SetLength(0); // Reset stream for each new message

        WebSocketReceiveResult result;
        // Read chunks until the complete message is received (handles fragmentation)
        do
        {
            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Closing by remote peer request", 
                    CancellationToken.None);
                return result;
            }

            memoryStream.Write(buffer, 0, result.Count);
        } 
        while (!result.EndOfMessage);
        
        return result;
    }  
}