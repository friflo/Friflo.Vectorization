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
    
    // \x1b[?1h  = Enable Application Cursor Keys (force terminal to send \x1bOA / \x1b[A)
    // \x1b[?25l = Hide Cursor (optional for cleaner TUI)
    public static ReadOnlySpan<byte> EnableRawTuiMode => "\x1b[?1h\x1b[?25l"u8;
    
    // ANSI Sequences
    private static readonly byte[] ClearScreen = "\x1b[2J\x1b[H"u8.ToArray();
    private static readonly byte[] ResetColor = "\x1b[0m"u8.ToArray();
    private static readonly byte[] HighlightColor = "\x1b[1;42;30m"u8.ToArray(); // Green background, black text

    // Menu options
    private static readonly string[] MenuItems = new[]
    {
        "  1. Start Application  ",
        "  2. Settings           ",
        "  3. Exit               "
    };

    public static async ValueTask ProcessClientNavigationAsync(Socket socket, CancellationToken cancellationToken)
    {
        int selectedIndex = 0;

        // Draw initial screen
        await RenderMenuAsync(socket, selectedIndex, cancellationToken);

        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int bytesRead = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break;

                ReadOnlySpan<byte> received = buffer.AsSpan(0, bytesRead);

                // Check for ANSI Arrow Key Sequences (\x1b[A and \x1b[B)
                if (received.Length >= 3 && received[0] == 0x1B && received[1] == 0x5B)
                {
                    bool stateChanged = false;

                    if (received[2] == 0x41) // Arrow Up
                    {
                        selectedIndex = (selectedIndex > 0) ? selectedIndex - 1 : MenuItems.Length - 1;
                        stateChanged = true;
                    }
                    else if (received[2] == 0x42) // Arrow Down
                    {
                        selectedIndex = (selectedIndex < MenuItems.Length - 1) ? selectedIndex + 1 : 0;
                        stateChanged = true;
                    }

                    // Redraw screen on navigation change
                    if (stateChanged)
                    {
                        await RenderMenuAsync(socket, selectedIndex, cancellationToken);
                    }
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async ValueTask RenderMenuAsync(Socket socket, int selectedIndex, CancellationToken cancellationToken)
    {
        // Clear screen and move cursor home
        await socket.SendAsync(ClearScreen, SocketFlags.None, cancellationToken);

        // Header
        byte[] header = Encoding.UTF8.GetBytes("\x1b[1;36m=== TUI NAVIGATION MENU ===\x1b[0m\r\nUse Up/Down Arrow keys to navigate.\r\n\r\n");
        await socket.SendAsync(header, SocketFlags.None, cancellationToken);

        // Render Menu Items
        for (int i = 0; i < MenuItems.Length; i++)
        {
            if (i == selectedIndex)
            {
                // Active item with highlight background
                await socket.SendAsync(HighlightColor, SocketFlags.None, cancellationToken);
                await socket.SendAsync(Encoding.UTF8.GetBytes($"> {MenuItems[i]} <\r\n"), SocketFlags.None, cancellationToken);
                await socket.SendAsync(ResetColor, SocketFlags.None, cancellationToken);
            }
            else
            {
                // Inactive item
                await socket.SendAsync(Encoding.UTF8.GetBytes($"  {MenuItems[i]}  \r\n"), SocketFlags.None, cancellationToken);
            }
        }
    }
}

