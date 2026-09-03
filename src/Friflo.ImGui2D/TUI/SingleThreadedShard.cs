namespace Friflo.TmGui.TUI;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;



public enum ClientEventType : byte { Connected, Disconnected, Input }

public readonly struct ClientEvent
{
    public required Socket Socket { get; init; }
    public required ClientEventType Type { get; init; }
    public ReadOnlyMemory<byte> Payload { get; init; }
}

public struct PlayerState
{
    public int      SelectedIndex;
    public byte[]   RenderBuffer;
}

public sealed class SingleThreadedShardEngine
{
    // Single reader channel guarantees zero-sync single-thread execution
    private readonly Channel<ClientEvent> _eventChannel = Channel.CreateUnbounded<ClientEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    // Raw non-thread-safe state (accessed exclusively by _shardThread)
    private readonly Dictionary<Socket, PlayerState> _shardState = new();
    private static readonly string[] MenuItems = ["Start Application", "Settings", "Exit"];

    public void Start()
    {
        Thread shardThread = new(RunEventLoop) { IsBackground = true, Name = "ShardLoopThread" };
        shardThread.Start();
    }

    public async ValueTask EnqueueEventAsync(Socket socket, ClientEventType type, ReadOnlyMemory<byte> payload = default)
    {
        await _eventChannel.Writer.WriteAsync(new ClientEvent { Socket = socket, Type = type, Payload = payload });
    }

    // Core event loop running strictly on a single thread
    private void RunEventLoop()
    {
        var reader = _eventChannel.Reader;
        while (reader.WaitToReadAsync().AsTask().Result)
        {
            while (reader.TryRead(out ClientEvent evt))
            {
                ProcessEvent(evt);
            }
        }
    }

    private void ProcessEvent(in ClientEvent evt)
    {
        switch (evt.Type)
        {
            case ClientEventType.Connected:
                var initialState = new PlayerState { SelectedIndex = 0 };
                _shardState[evt.Socket] = initialState;
                
                // Render full menu for new client
                _ = TuiTestMenu.RenderFullMenuAsync(evt.Socket, initialState.SelectedIndex);
                break;

            case ClientEventType.Disconnected:
                _shardState.Remove(evt.Socket);
                break;

            case ClientEventType.Input:
                if (_shardState.TryGetValue(evt.Socket, out PlayerState state))
                {
                    ReadOnlySpan<byte> input = evt.Payload.Span;

                    if (input.Length >= 3 && input[0] == 0x1B && input[1] == 0x5B)
                    {
                        int oldIndex = state.SelectedIndex;
                        int maxItems = TuiTestMenu.MenuItems.Length;

                        if (input[2] == 0x41) // Arrow Up
                            state.SelectedIndex = (state.SelectedIndex > 0) ? state.SelectedIndex - 1 : maxItems - 1;
                        else if (input[2] == 0x42) // Arrow Down
                            state.SelectedIndex = (state.SelectedIndex < maxItems - 1) ? state.SelectedIndex + 1 : 0;

                        if (oldIndex != state.SelectedIndex)
                        {
                            _shardState[evt.Socket] = state;
                            
                            // Differential update without full screen clear
                            _ = TuiTestMenu.UpdateMenuSelectionAsync(evt.Socket, oldIndex, state.SelectedIndex);
                        }
                    }
                }
                break;
        }
    }

    // Differential rendering logic without lock overhead
    private static void RenderAndSend(Socket socket, ref PlayerState state, bool isFullRedraw, int oldIndex = 0)
    {
        if (isFullRedraw)
        {
            string fullScreen = "\x1b[2J\x1b[H\x1b[1;36m=== TUI SHARD SYSTEM ===\x1b[0m\r\n\r\n";
            for (int i = 0; i < MenuItems.Length; i++)
                fullScreen += FormatItem(i, i == state.SelectedIndex);

            _ = socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(fullScreen), SocketFlags.None);
            return;
        }

        // Differential update: reposition cursor and update affected lines only
        string diffUpdate = $"\x1b[{6 + oldIndex};1H{FormatItem(oldIndex, false)}" +
                            $"\x1b[{6 + state.SelectedIndex};1H{FormatItem(state.SelectedIndex, true)}";

        _ = socket.SendAsync(System.Text.Encoding.UTF8.GetBytes(diffUpdate), SocketFlags.None);
    }

    private static string FormatItem(int index, bool isSelected) =>
        isSelected ? $"\x1b[1;42;30m> {MenuItems[index]} <\x1b[0m\x1b[K\r\n" : $"  {MenuItems[index]}  \x1b[K\r\n";
}