// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;


// ReSharper disable RedundantLambdaParameterType
// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public static class TmSignalingExtensions
{
    public static void UseSignaling(this IApplicationBuilder app, TmSignaling signaling)
    {
        app.UseWebSockets();
        
        var handler = new SignalSocketHandler(signaling);
        app.Use(handler.HandleSignalRequestAsync);
    }
}

internal sealed class SignalSocketHandler
{
    private readonly    TmSignaling signaling;

    internal SignalSocketHandler(TmSignaling signaling)
    {
        this.signaling = signaling;
    }
    
    private static WebSocketAcceptContext CreateWebSocketAcceptContext()
    {
        // Enable permessage-deflate compression for this connection
        return new WebSocketAcceptContext {
            DangerousEnableCompression  = true,
        }; 
    }
    
    private int connectionId;

    internal async Task HandleSignalRequestAsync(HttpContext context, Func<Task> next)
    {
        var path = context.Request.Path;
        
        if (context.WebSockets.IsWebSocketRequest && path.StartsWithSegments("/signaling"))
        {
            try
            {
                var acceptContext = CreateWebSocketAcceptContext();
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync(acceptContext);
                
                var id = Interlocked.Increment(ref connectionId);
                var contextConn  = context.Connection;
                
                var connection  = $"{id}|{contextConn.RemoteIpAddress?.ToString()}:{contextConn.RemotePort}";
                var peer        = new TmSignalPeer(webSocket, connection);
                
                await HandlePeerSessionAsync(peer, signaling, context.RequestAborted);
            }
            catch (Exception ex)
            {
                // ReSharper disable once MethodHasAsyncOverload
                // Console.Error.WriteLine($"[-] WebSocket error: {ex.Message}");
            }
            return;
        }
        if (path.StartsWithSegments("/textures", out var remainingPath) && remainingPath.HasValue)
        {
            var response = context.Response;

            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    }
    
    private static async Task HandlePeerSessionAsync(TmSignalPeer peer, TmSignaling signaling, CancellationToken cancellationToken)
    {
        // Allocate a reusable buffer for incoming websocket frames
        var buffer = new byte[1024 * 4];

        try
        {
            while (peer.webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                // Receive incoming data segment asynchronously
                var result = await peer.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                // Handle graceful close request from client
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await peer.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing by client request", cancellationToken);
                    break;
                }

                // Handle text messages (typically JSON payloads for WebRTC SDP/ICE signaling)
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = buffer.AsMemory(0, result.Count);
                    await signaling.ProcessMessageAsync(peer, message);
                    
                    // Process the signaling message
                    // await signaling.ProcessMessageAsync(peer, message);
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
            if (peer.webSocket.State is WebSocketState.Open or WebSocketState.Aborted)
            {
                var app = signaling.guiApps.Values.FirstOrDefault(app => app.peer == peer);
                if (app != null) {
                    app.online = false;
                    signaling.SetDirty();
                }
                peer.webSocket.Dispose();
            }
        }
    }
}