// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Friflo.TmGui.TUI.Terminal;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.TUI.attic;


public class TuiTerminalServer
{
    private readonly    int                                 port;
    private readonly    ConcurrentDictionary<Socket, byte>  clients = new();
    private             Socket?                             listenSocket;
    private             CancellationTokenSource?            cts;
    
    private static readonly SingleThreadedShardEngine EngineSingleton = new SingleThreadedShardEngine();

    public TuiTerminalServer(int port)
    {
        this.port = port;
    }

    public async Task StartAsync()
    {
        cts = new CancellationTokenSource();
        listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
        listenSocket.Listen(100);

        Console.WriteLine($"Zero-allocation server listening on port {port}...");

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                // Connect/Accept is allowed to allocate
                Socket clientSocket = await listenSocket.AcceptAsync(cts.Token);
                clients.TryAdd(clientSocket, 0);

                // Fire-and-forget without creating a Task object
                _ = ProcessClientAsync(clientSocket, clients, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Server stopping...");
        }
    }

    public void Stop()
    {
        cts?.Cancel();
        listenSocket?.Close();

        foreach (var kvp in clients)
        {
            kvp.Key.Close();
        }
        clients.Clear();
    }

    // Marked static to prevent closure allocation; returns ValueTask
    private static async ValueTask ProcessClientAsync(Socket socket, ConcurrentDictionary<Socket, byte> clients, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[+] Client connected: {socket.RemoteEndPoint}");
        
        // Send Hello World Screen with ANSI colors upon connect (zero-allocation)
        
        // ReadOnlyMemory<byte> rawTuiMode = TuiSession.EnableRawTuiMode.ToArray();
        // await socket.SendAsync(rawTuiMode, SocketFlags.None, cancellationToken);
        
        // ReadOnlyMemory<byte> screenData = TuiSession.HelloWorldScreen.ToArray(); // Once per connect
        // await socket.SendAsync(screenData, SocketFlags.None, cancellationToken);

        // Rent a buffer from the shared pool for reading raw bytes (No allocation)
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // ValueTask avoids allocation when reading asynchronously
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break; // Client disconnected

                // ReadOnlyMemory<byte> receivedMemory = buffer.AsMemory(0, bytesRead);
                
                await TuiTestMenu.HandleClientSessionAsync(socket, EngineSingleton, cancellationToken);
                
                // Zero-allocation broadcast
                // await BroadcastAsync(clients, receivedMemory, cancellationToken);
            }
        }
        catch (Exception)
        {
            // Socket errors or disconnects
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            clients.TryRemove(socket, out _);
            Console.WriteLine($"[-] Client disconnected: {socket.RemoteEndPoint}");
            socket.Close();
        }
    }

    // Iterates direct KeyValuePairs using struct enumerator to avoid Boxing
    private static async ValueTask BroadcastAsync(ConcurrentDictionary<Socket, byte> clients, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        foreach (var kvp in clients)
        {
            try
            {
                // ValueTask write avoiding allocations
                await kvp.Key.SendAsync(data, SocketFlags.None, cancellationToken);
            }
            catch (Exception)
            {
                // Failed writes will be cleaned up by ProcessClientAsync when disconnected
            }
        }
    }
}

