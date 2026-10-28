// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Friflo.SmGui.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

// ReSharper disable RedundantLambdaParameterType
// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Http;


public static class SmGuiHttpExtensions
{
    private static readonly string  WebRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

    
    public static void UseWebSocketGuiSessions(this IApplicationBuilder app, SmSessionLoop loop, string? webRoot = null)
    {
        webRoot ??= WebRoot;
        
        // Set up custom MIME types (especially for .wgsl WebGPU shaders)
        var provider = new FileExtensionContentTypeProvider {
            Mappings = {
                [".wgsl"] = "text/wgsl; charset=utf-8"
            }
        };

        app.UseWebSockets();
        
        var guiWebHandler = new HttpGuiHandler(loop);
        app.Use(guiWebHandler.HandleGuiRequestAsync);

        // Serve Static Files from wwwroot with custom ContentTypes
        if (Directory.Exists(webRoot))
        {
            var staticFileOptions = new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot),
                ContentTypeProvider = provider,
                RequestPath = ""
            };

            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(webRoot),
                DefaultFileNames = ["index.html"]
            });

            app.UseStaticFiles(staticFileOptions);
        }
    }
}

public class HttpGuiHandler
{
    private readonly SmSessionLoop loop;

    public HttpGuiHandler(SmSessionLoop loop)
    {
        this.loop = loop;
    }
    
    private static WebSocketAcceptContext CreateWebSocketAcceptContext()
    {
        // Check websocket compression:
        // * TCPView - simple setup / live send/receive bytes
        // * Chrome
        //   - open:  chrome://net-export/
        //   - log to file for ~2 seconds
        //   - click small link at The log file can be loaded using the >> netlog_viewer <<.
        //   - Choose File
        //   - Navigate in left Panel > Sockets > View live sockets
        //   - Filter:    websocket
        //   - Click:     URL_REQUEST
        //   It will show logs like
        //     t=580907 [st=41780]  HTTP2_STREAM_UPDATE_RECV_WINDOW
        //                          --> delta = -7554
        //                          --> stream_id = 5
        //                          --> window_size = 6283902
        
        // Enable permessage-deflate compression for this connection
        return new WebSocketAcceptContext {
            DangerousEnableCompression  = true,
            ServerMaxWindowBits         = 12 // 2^12  => 4 KB per client. 4 KB window aligns with standard OS memory page sizes
        }; 
    }

    public async Task HandleGuiRequestAsync(HttpContext context, Func<Task> next)
    {
        var path = context.Request.Path;
        
        if (context.WebSockets.IsWebSocketRequest && path == "/ws")
        {
            try
            {
                var acceptContext = CreateWebSocketAcceptContext();
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync(acceptContext);
                var client = new WebSocketClient(webSocket);
                await WebSocketClient.HandleClientSessionAsync(client, loop, context.RequestAborted);
            }
            catch (Exception ex)
            {
                // ReSharper disable once MethodHasAsyncOverload
                Console.Error.WriteLine($"[-] WebSocket error: {ex.Message}");
            }
            return;
        }
        if (path.HasValue && path.StartsWithSegments(AssetResources.AssetsPath))
        {
            var fileName = path.Value.Substring(AssetResources.AssetsPath.Length + "/".Length);
            
            var response = context.Response;
            if (loop.resources.TryGetImageResource(fileName, out var image))
            {
                if (context.Request.Headers.IfNoneMatch == image.Etag) {
                    response.StatusCode = StatusCodes.Status304NotModified;
                    return;
                }
                var pngData = image.GetAsPng();
                response.Headers.ETag   = image.Etag;
                response.ContentType    = "image/png";
                response.ContentLength  = pngData.Length;
                response.StatusCode     = StatusCodes.Status200OK;
                await response.Body.WriteAsync(pngData, context.RequestAborted);
                return;
            }
            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    }
}