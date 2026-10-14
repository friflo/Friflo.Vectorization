// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;


// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;



internal partial class SignalClient
{

    internal static async Task HandleClientSessionAsync(SignalClient client, TmSignaling signaling, CancellationToken cancellationToken)
    {
        // Allocate a reusable buffer for incoming websocket frames
        var buffer = new byte[1024 * 4];

        try
        {
            while (client.webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                // Receive incoming data segment asynchronously
                var result = await client.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                // Handle graceful close request from client
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, 
                        "Closing by client request", 
                        cancellationToken);
                    break;
                }

                // Handle text messages (typically JSON payloads for WebRTC SDP/ICE signaling)
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = buffer.AsSpan(0, result.Count);
                    signaling.ProcessMessage(client, message);
                    
                    // Process the signaling message
                    // await signaling.ProcessMessageAsync(client, buffer);
                }
            }
        }
        catch (WebSocketException)
        {
            // Handle abrupt disconnections or network drops gracefully without crashing the server
        }
        finally
        {
            // Ensure socket resources are cleaned up when the loop exits
            if (client.webSocket.State == WebSocketState.Open || client.webSocket.State == WebSocketState.Aborted)
            {
                client.webSocket.Dispose();
            }
        }
    }
}