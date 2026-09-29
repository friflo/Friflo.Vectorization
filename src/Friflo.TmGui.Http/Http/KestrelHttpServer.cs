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
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public sealed class KestrelHttpServer
{
    private readonly string        webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
    private readonly TmSessionLoop loop;
    private readonly int           port;

    private IHost?                 host;
    private Task?                  serverTask;

    public KestrelHttpServer(TmSessionLoop loop, int port)
    {
        this.loop = loop;
        this.port = port;
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
            // Set up custom MIME types (especially for .wgsl WebGPU shaders)
            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".wgsl"] = "text/wgsl; charset=utf-8";

            var builder = Host.CreateDefaultBuilder();

            // Disable verbose framework logging for high performance
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseKestrel(options =>
                {
                    // Binding solely to localhost (no admin privileges required)
                    // - IPAddress.Loopback for localhost only
                    options.Listen(IPAddress.Any, port, listenOptions => {  
                        listenOptions.UseHttps(); // uses automatically dotnet dev-certs
                    });
                    
                    // Optional: If remote network access is needed:
                    // options.Listen(IPAddress.Any, port);
                });

                webBuilder.Configure(app =>
                {
                    // 1. Enable WebSockets middleware
                    app.UseWebSockets();

                    // 2. Intercept WebSocket requests
                    app.Use(async (context, next) =>
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            try
                            {
                                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                                var client = new WebSocketClient(webSocket);
                                
                                // RequestAborted acts as CancellationToken when connection drops or server shuts down
                                await WebSocketClient.HandleClientSessionAsync(client, loop, context.RequestAborted);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine($"[-] WebSocket error: {ex.Message}");
                            }
                        }
                        else
                        {
                            await next();
                        }
                    });

                    // 3. Serve Static Files from wwwroot with custom ContentTypes
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
                            DefaultFileNames = new[] { "index.html" }
                        });

                        app.UseStaticFiles(staticFileOptions);
                    }
                });
            });

            host = builder.Build();

            Console.WriteLine($"[+] Kestrel Server active on port {port}...");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[-] Critical Server error: {ex.Message}");
        }
    }
}