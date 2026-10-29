// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Session;


public sealed class SocketClient : SmClient
{
    private  Socket? socket;
    
    public SocketClient(Socket socket, SmSessionLoop loop) : base(loop)
    {
        this.socket = socket;
    }
    
    public override void Dispose() {
        var sock = socket;
        if (sock == null) return;
        socket = null;
        try {
            // Send clear shutdown signal (TCP FIN)
            sock.Shutdown(SocketShutdown.Both);
        } catch (SocketException) {
            // Catch error if connection was already closed by client
        } finally {
            sock.Close();
        }
    }
    
    public override UiType UiType => UiType.TerminalTui;
    
    protected internal override async ValueTask<int> SendAsync(SmSendMessage message, CancellationToken cancellationToken)
    {
        var sock = socket;
        if (sock == null) return 0;
        return await sock.SendAsync(message.buffer, SocketFlags.None, cancellationToken);
    }
    
    protected internal override int Send(SmSendMessage message)
    {
        var sock = socket;
        if (sock == null) return 0;
        // Check if the OS send buffer can accept data immediately (0 microseconds wait time)
        if (!sock.Poll(0, SelectMode.SelectWrite)) {
            return 0;                                       // OS buffer is full -> drop frame instantly to prevent blocking the single thread loop
        }
        return sock.Send(message.buffer.Span, SocketFlags.None);  // Buffer has space -> transmit frame synchronously without blocking
    }
    
    protected internal override void SendCommand(SmSendCommand command) { }
    
    protected internal override void RestoreTerminal()
    {
        Send(new SmSendMessage(VT100.TerminalReset));
    }
    
    
    // I/O Loop: Reads raw socket bytes and pushes them into the session loop queue
    public async ValueTask HandleTerminalIOAsync(CancellationToken cancellationToken = default)
    {
        var sock = socket;
        if (sock == null) return;
        try
        {
            Payload initialPayload = default;
            if (sock.Available > 0) {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int initialBytes = await sock.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (initialBytes > 0) {
                    initialPayload = new Payload(buffer, initialBytes);
                }
            }

            // Notify loop about new client connection, passing initial payload (if any)
            TryEnqueueEvent(LoopEventType.TerminalConnected, this, initialPayload);

            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
                int bytesRead = await sock.ReceiveAsync(buffer.AsMemory(), SocketFlags.None, cancellationToken);
                if (bytesRead == 0) break;

                var payload = new Payload(buffer, bytesRead);

                // Forward raw input directly to the shard event loop
                TryEnqueueClientEvent(LoopEventType.TerminalInput, payload);
            }
        }
        finally
        {
            // ArrayPool<byte>.Shared.Return(buffer);
            TryEnqueueClientEvent(LoopEventType.TerminalDisconnected, default);
        }
    }
}