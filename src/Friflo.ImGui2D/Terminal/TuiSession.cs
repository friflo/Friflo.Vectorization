// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D.Terminal;




public class TuiSession
{
    // Pre-encoded UTF-8 byte array with ANSI sequences for colored UI
    public static ReadOnlySpan<byte> HelloWorldScreen => """
        [2J[H[1;36m==============================================[0m
        [1;33m             TUI TERMINAL SERVER              [0m
        [1;36m==============================================[0m

        [1;32m  HELLO WORLD![0m

        Welcome to the zero-allocation ANSI terminal.
        Connected via [1;35mncat[0m over local network.

        [1;30mStatus: [1;32mOnline [1;30m| Port: [1;33m9000[0m
        [1;36m----------------------------------------------[0m
        Type a message and press ENTER to broadcast:
        > 
        """u8;

    // Terminal setup sequences
    public static ReadOnlySpan<byte> EnableRawTuiMode => "\x1b[?1h\x1b[?25l"u8;

    // ANSI Sequences for Menu Rendering
    public static readonly byte[] ClearScreen = "\x1b[2J\x1b[H"u8.ToArray();
    public static readonly byte[] ResetColor = "\x1b[0m"u8.ToArray();
    public static readonly byte[] HighlightColor = "\x1b[1;42;30m"u8.ToArray(); // Green background, black text
    public static readonly byte[] EraseInLine = "\x1b[K"u8.ToArray();

    // Menu options & positioning
    public const int MenuStartRow = 5;
    public static readonly string[] MenuItems = new[]
    {
        "1. Start Application",
        "2. Settings",
        "3. Exit"
    };

    // I/O Loop: Reads raw socket bytes and pushes them into the single-threaded engine queue
    public static async ValueTask HandleClientSessionAsync(Socket socket, SingleThreadedShardEngine engine, CancellationToken cancellationToken)
    {
        // Enable raw mode on client terminal
        await socket.SendAsync(EnableRawTuiMode.ToArray(), SocketFlags.None, cancellationToken);

        // Notify engine about new client connection
        await engine.EnqueueEventAsync(socket, ClientEventType.Connected);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break;

                ReadOnlyMemory<byte> payload = buffer.AsMemory(0, bytesRead);

                Console.WriteLine($"received [{bytesRead}] text: {Encoding.UTF8.GetString(payload.Span)}");

                // Forward raw input directly to the shard event loop
                await engine.EnqueueEventAsync(socket, ClientEventType.Input, payload);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            await engine.EnqueueEventAsync(socket, ClientEventType.Disconnected);
        }
    }

    // Full screen initial render
    public static async ValueTask RenderFullMenuAsync(Socket socket, int selectedIndex, CancellationToken cancellationToken = default)
    {
        await socket.SendAsync(ClearScreen, SocketFlags.None, cancellationToken);

        byte[] header = Encoding.UTF8.GetBytes("\x1b[1;36m=== TUI NAVIGATION MENU ===\x1b[0m\r\nUse Up/Down Arrow keys to navigate.\r\n\r\n");
        await socket.SendAsync(header, SocketFlags.None, cancellationToken);

        for (int i = 0; i < MenuItems.Length; i++)
        {
            await RenderMenuItemAsync(socket, i, isSelected: (i == selectedIndex), cancellationToken);
        }
    }

    // Flicker-free differential update (updates only changing menu items)
    public static async ValueTask UpdateMenuSelectionAsync(Socket socket, int oldIndex, int newIndex, CancellationToken cancellationToken = default)
    {
        await RenderMenuItemAsync(socket, oldIndex, isSelected: false, cancellationToken);
        await RenderMenuItemAsync(socket, newIndex, isSelected: true, cancellationToken);
    }

    // Helper method to draw a single menu line at a precise row
    private static async ValueTask RenderMenuItemAsync(Socket socket, int index, bool isSelected, CancellationToken cancellationToken)
    {
        int targetRow = MenuStartRow + index;

        // Position cursor at specific line
        string moveCursor = $"\x1b[{targetRow};1H";
        await socket.SendAsync(Encoding.UTF8.GetBytes(moveCursor), SocketFlags.None, cancellationToken);

        if (isSelected)
        {
            await socket.SendAsync(HighlightColor, SocketFlags.None, cancellationToken);
            await socket.SendAsync(Encoding.UTF8.GetBytes($"> {MenuItems[index]} <"), SocketFlags.None, cancellationToken);
            await socket.SendAsync(ResetColor, SocketFlags.None, cancellationToken);
        }
        else
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes($"  {MenuItems[index]}  "), SocketFlags.None, cancellationToken);
        }

        await socket.SendAsync(EraseInLine, SocketFlags.None, cancellationToken);
    }
}