// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
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
    public static void UseSignaling(this IApplicationBuilder app, Signaling signaling)
    {
        app.UseWebSockets();
        
        var handler = new SignalingSocketHandler(signaling);
        app.Use(handler.HandleSignalRequestAsync);
    }
}

public class SignalingSocketHandler
{
    private readonly Signaling signaling;

    public SignalingSocketHandler(Signaling signaling)
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

    public async Task HandleSignalRequestAsync(HttpContext context, Func<Task> next)
    {
        var path = context.Request.Path;
        
        if (context.WebSockets.IsWebSocketRequest && path.StartsWithSegments("/signaling"))
        {
            try
            {
                var acceptContext = CreateWebSocketAcceptContext();
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync(acceptContext);
                
                var connection  = context.Connection;
                var clientId    = $"{connection.Id}|{connection.RemoteIpAddress?.ToString()}:{connection.RemotePort}";
                var client      = new SignalingClient(webSocket, clientId);
                await SignalingClient.HandleClientSessionAsync(client, signaling, context.RequestAborted);
            }
            catch (Exception ex)
            {
                // ReSharper disable once MethodHasAsyncOverload
                Console.Error.WriteLine($"[-] WebSocket error: {ex.Message}");
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
}