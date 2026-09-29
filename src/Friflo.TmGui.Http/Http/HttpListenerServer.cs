// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session.HTTP;

public sealed class HttpListenerServer
{
    private readonly string             webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
    private readonly TmSessionLoop      loop;
    private readonly int                port;
    
    private CancellationTokenSource?    cts;
    private Task?                       serverTask;

    public HttpListenerServer(TmSessionLoop loop, int port)
    {
        this.loop = loop;
        this.port = port;
    }

    /// <summary>
    /// Starts the HTTP/WebSocket listener on a dedicated background thread.
    /// </summary>
    public void Start()
    {
        if (serverTask != null) return;

        cts = new CancellationTokenSource();
        
        // TaskCreationOptions.LongRunning ensures the scheduler spawns a dedicated OS Thread
        serverTask = Task.Factory.StartNew(
            () => RunAsync(cts.Token),
            cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        ).Unwrap();
    }

    /// <summary>
    /// Stops the server gracefully.
    /// </summary>
    public async Task StopAsync()
    {
        if (cts == null || serverTask == null) return;

        cts.Cancel();
        try
        {
            await serverTask;
        }
        catch (OperationCanceledException) { }
        finally
        {
            cts.Dispose();
            cts = null;
            serverTask = null;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var listener = new HttpListener();
        // listener.Prefixes.Add($"http://*:{port}/");
        listener.Prefixes.Add($"http://localhost:{port}/");
        
        try
        {
            listener.Start();
            Console.WriteLine($"[+] HTTP/WebSocket Listener active on port {port}...");

            // Unregister listener on cancellation to unblock GetContextAsync
            using var reg = cancellationToken.Register(() => listener.Stop());

            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync();
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break; // Listener stopped via cancellation
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (context.Request.IsWebSocketRequest)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                            var client = new WebSocketClient(wsContext.WebSocket);
                            await WebSocketClient.HandleClientSessionAsync(client, loop, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[-] WebSocket error: {ex.Message}");
                        }
                    }, cancellationToken);
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ServeStaticFileAsync(context);
                        }
                        catch (Exception ex)
                        {
                            Console.Error.WriteLine($"[-] Static File error: {ex.Message}");
                        }
                    }, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine($"[-] Critical Server error: {ex.Message}");
        }
    }

    private async Task ServeStaticFileAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        string rawPath = request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;
        if (string.IsNullOrEmpty(rawPath))
        {
            rawPath = "index.html";
        }

        string filePath = Path.GetFullPath(Path.Combine(webRoot, rawPath));

        if (!filePath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = (int)HttpStatusCode.Forbidden;
            response.Close();
            return;
        }

        if (!File.Exists(filePath))
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        response.ContentType = GetContentType(Path.GetExtension(filePath));

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        response.ContentLength64 = fileStream.Length;
        await fileStream.CopyToAsync(response.OutputStream);
        response.OutputStream.Close();
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".js" or ".mjs"   => "text/javascript; charset=utf-8",
        ".wgsl"           => "text/wgsl; charset=utf-8",
        ".css"            => "text/css; charset=utf-8",
        ".json"           => "application/json; charset=utf-8",
        ".png"            => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif"            => "image/gif",
        ".svg"            => "image/svg+xml",
        ".ico"            => "image/x-icon",
        ".wasm"           => "application/wasm",
        ".ttf"            => "font/ttf",
        ".woff"           => "font/woff",
        ".woff2"          => "font/woff2",
        _                 => "application/octet-stream"
    };
}