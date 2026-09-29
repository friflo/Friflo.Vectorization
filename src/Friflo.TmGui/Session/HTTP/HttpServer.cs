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

public static class TmHttpServer
{
    private static readonly string WebRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot");

    public static async Task RunHttpServerAsync(TmSessionLoop loop, int port)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://*:{port}/");
        listener.Start();

        Console.WriteLine($"[+] HTTP/WebSocket Listener active on port {port}...");

        while (true)
        {
            HttpListenerContext context = await listener.GetContextAsync();

            if (context.Request.IsWebSocketRequest)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Accept WebSocket handshake
                        var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                        
                        var client = new WebSocketClient(wsContext.WebSocket);
                        await WebSocketClient.HandleClientSessionAsync(client, loop, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        // Log or handle handshake/session exceptions quietly
                        Console.Error.WriteLine($"[-] WebSocket error: {ex.Message}");
                    }
                });
            }
            else
            {
                // Serve static files asynchronously
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
                });
            }
        }
    }

    private static async Task ServeStaticFileAsync(HttpListenerContext context)
    {
        var request  = context.Request;
        var response = context.Response;

        // Resolve local file path and map root '/' to 'index.html'
        string rawPath = request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty;
        if (string.IsNullOrEmpty(rawPath))
        {
            rawPath = "index.html";
        }

        var filePath = Path.GetFullPath(Path.Combine(WebRoot, rawPath));

        // Prevent directory traversal attacks (e.g. '/../../secret.txt')
        if (!filePath.StartsWith(WebRoot, StringComparison.OrdinalIgnoreCase))
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

        // Set MIME type
        response.ContentType = GetContentType(Path.GetExtension(filePath));

        // Open file using high-performance FileOptions.Asynchronous
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
