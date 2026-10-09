// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.Session;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ReSharper disable RedundantLambdaParameterType
// ReSharper disable ConvertClosureToMethodGroup
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public sealed class KestrelHttpServer
{
    private readonly    string          webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
    private readonly    TmSessionLoop   loop;
    private readonly    EndPoint        endPoint;

    private             IHost?          host;
    private             Task?           serverTask;

    public KestrelHttpServer(TmSessionLoop loop, EndPoint endPoint)
    {
        this.loop       = loop;
        this.endPoint   = endPoint;
    }

    /// <summary>
    /// Starts the Kestrel server asynchronously on a dedicated background task.
    /// </summary>
    public void Start()
    {
        if (serverTask != null) return;

        // Dedicated background thread execution equivalent to TaskCreationOptions.LongRunning
        serverTask = Task.Factory.StartNew(
            RunAsync,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        ).Unwrap();
    }

    /// <summary>
    /// Stops the server gracefully.
    /// </summary>
    public async Task StopAsync()
    {
        if (host == null) return;

        try
        {
            await host.StopAsync();
        }
        catch (OperationCanceledException) { }
        finally
        {
            host.Dispose();
            host = null;
            serverTask = null;
        }
    }

    private async Task RunAsync()
    {
        try
        {
            var builder = Host.CreateDefaultBuilder();

            // Disable verbose framework logging for high performance
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseSockets(socketOptions => {
                    // deactivate Nagle-algorithm for minimal websocket latency
                    socketOptions.NoDelay = true;
                });
                webBuilder.UseKestrel(options => {
                    options.Listen(endPoint, listenOptions => {  
                        listenOptions.UseHttps(); // uses automatically dotnet dev-certs
                    });
                });

                webBuilder.Configure(app => {
                    Configure(app, loop, webRoot);
                });
            });

            host = builder.Build();

            Console.WriteLine($"[+] Kestrel Server listening on: {endPoint} ...");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            // ReSharper disable once MethodHasAsyncOverload
            Console.Error.WriteLine($"[-] Critical Server error: {ex.Message}");
        }
    }
    
    public static void Configure(IApplicationBuilder app, TmSessionLoop loop, string webRoot)
    {
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
    private readonly TmSessionLoop loop;

    public HttpGuiHandler(TmSessionLoop loop)
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
        
        if (context.WebSockets.IsWebSocketRequest)
        {
            try
            {
                var acceptContext = CreateWebSocketAcceptContext(); // (oder wo auch immer das herkommt)
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
        if (path.StartsWithSegments("/textures", out var remainingPath) && remainingPath.HasValue)
        {
            string fileName = remainingPath.Value.TrimStart('/');
            
            if (loop.resources.stringToImage.TryGetValue(fileName, out var image))
            {
                if (context.Request.Headers.IfNoneMatch == image.Etag) {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    return;
                }
                var pngData = image.GetAsPng();
                context.Response.Headers.ETag   = image.Etag;
                context.Response.ContentType    = "image/png";
                context.Response.ContentLength  = pngData.Length;
                context.Response.StatusCode     = StatusCodes.Status200OK;
                await context.Response.Body.WriteAsync(pngData, context.RequestAborted);
                return;
            }
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    }
}