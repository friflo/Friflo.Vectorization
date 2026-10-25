// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Friflo.SmGui.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Http;


public static class HttpUtils
{
    /// <summary>
    /// Starts the Kestrel server asynchronously on a dedicated background task.
    /// </summary>
    public static Task Start(IHost host)
    {
        // Dedicated background thread execution equivalent to TaskCreationOptions.LongRunning
        var serverTask = Task.Factory.StartNew(
            () => RunAsync(host),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        ).Unwrap();
        return serverTask;
    }
    
    private static async Task RunAsync(IHost host)
    {
        try
        {
            Console.WriteLine($"[+] Kestrel Server listening ...");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            // ReSharper disable once MethodHasAsyncOverload
            Console.Error.WriteLine($"[-] Critical Server error: {ex.Message}");
        }
    }
    
    public static IHostBuilder CreateHostBuilder(EndPoint endPoint, SmSessionLoop loop, string? webRoot = null)
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
                app.UseSessionLoop(loop, webRoot);
            });
        });

        return builder;
    }
}