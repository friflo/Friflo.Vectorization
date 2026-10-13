// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;


// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.WebRTC.WebRTC;

public partial class WebRtcListener
{
    private readonly TmSessionLoop loop;
        
    public WebRtcListener(TmSessionLoop loop)
    {
        this.loop = loop;
    }
        
    public Task Start(Uri uri)
    {
        // Dedicated background thread execution equivalent to TaskCreationOptions.LongRunning
        var serverTask = Task.Factory.StartNew(
            () => Run(uri),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        ).Unwrap();
        return serverTask;
    }
        
    private async Task Run(Uri uri, CancellationToken cancellationToken = default)
    {
        using var clientWebSocket = new ClientWebSocket();

        // Connect to the specified signaling server URL
        await clientWebSocket.ConnectAsync(uri, cancellationToken);
        
        
        ReadOnlyMemory<byte> payload = """
                                       {
                                            "type": "register",
                                            "host": "test-host"
                                       }
                                       """u8.ToArray();
        await clientWebSocket.SendAsync(payload, WebSocketMessageType.Text, true, cancellationToken);

        // Run the receive loop using the connected websocket
        await RunWebSocketReceiveLoopAsync(clientWebSocket, cancellationToken);
    }
    
    private async Task RunWebSocketReceiveLoopAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        // Reusable buffer for incoming data chunks
        var buffer = new byte[1024 * 4];
        using var memoryStream = new MemoryStream();

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
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
                        return;
                    }

                    memoryStream.Write(buffer, 0, result.Count);
                } 
                while (!result.EndOfMessage);

                // Process the complete message payload based on its type
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageBuffer = memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length);
                    
                    // Handle incoming signaling message (e.g., SDP offer/answer or ICE candidate)
                    ProcessSignalingMessage(messageBuffer);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Handle binary payloads if needed
                    // byte[] binaryData = memoryStream.ToArray();
                }
            }
        }
        catch (WebSocketException)
        {
            // Handle unexpected drops or client disconnects gracefully
        }
    }
    

}