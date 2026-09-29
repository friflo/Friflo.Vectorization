// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Friflo.TmGui.Session.HTTP;

internal sealed class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    internal            IGuiView?       guiView;
    internal readonly   WsBackend       wsBackend;
    private  readonly   WsBatch         wsBatch;        // instance: creates / owns
    private             WsDrawCommand[] wsDrawList      = [];
    private             byte[]          sendBuffer      = [];
    private             int             canvasWidth     = 500;
    private             int             canvasHeight    = 300;
    
    
    internal GuiSession(TmClient client, IGuiAssets assets)
    {
        wsBackend   = new WsBackend(assets);
        wsBatch     = wsBackend.CreateBatch();
        this.client = client;
    }

    public override void ProcessInput(ReadOnlySpan<byte> utf8Bytes)
    {
        Span<char> chars = stackalloc char[utf8Bytes.Length];
        int charCount = Encoding.UTF8.GetChars(utf8Bytes, chars);
        ReadOnlySpan<char> span = chars.Slice(0, charCount);

        // Max 16 Key-Value Paare auf dem Stack zulassen
        Span<Range> ranges = stackalloc Range[16];
        int count = span.Split(ranges, ';', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<char> pair = span[ranges[i]];

            int eqIndex = pair.IndexOf('=');
            if (eqIndex < 0) continue;

            ReadOnlySpan<char> key = pair.Slice(0, eqIndex);
            ReadOnlySpan<char> value = pair.Slice(eqIndex + 1);

            DispatchParam(key, value);
        }
    }
    
    private void DispatchParam(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        if (key.SequenceEqual("w") && int.TryParse(value, out canvasWidth)) {
        }
        else if (key.SequenceEqual("h") && int.TryParse(value, out canvasHeight)) {
        }
    }

    public override Memory<byte> IterateTui()
    {
        guiView!.RenderGui(wsBatch, canvasWidth, canvasHeight);

        var drawCommands = wsBatch.drawCommands;

        if (wsDrawList.Length < drawCommands.Count) {
            wsDrawList = new WsDrawCommand [drawCommands.Count];
        }
        for (int n = 0; n < drawCommands.Count; n++)
        {
            var cmd = drawCommands[n];
            wsDrawList[n] = new WsDrawCommand {
                vertexView  = cmd.vertexView,
                projection  = cmd.projection,
                scissor     = cmd.scissor,
            };
        }
        var vertices = wsBatch.Vertices;
        
        var sendLength = 4 + drawCommands.Count  * Unsafe.SizeOf<WsDrawCommand>() +
                         4 + vertices.Length     * Unsafe.SizeOf<Vertex2D>();
        if (sendBuffer.Length < sendLength) {
            sendBuffer = new byte[sendLength];
        }
        Span<byte> span = sendBuffer;
        int bytesWritten = 0;

        // 1. Write drawCommands.Count (int)
        MemoryMarshal.Write(span[bytesWritten..], drawCommands.Count);
        bytesWritten += sizeof(int);
        
        // 2. Write vertices.Length (int)
        MemoryMarshal.Write(span[bytesWritten..], vertices.Length);
        bytesWritten += sizeof(int);

        // 3. Write wsDrawList elements
        var drawListBytes = MemoryMarshal.AsBytes(wsDrawList.AsSpan(0, drawCommands.Count));
        drawListBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += drawListBytes.Length;

        // 4. Write vertices elements
        var vertexBytes = MemoryMarshal.AsBytes(vertices);
        vertexBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += vertexBytes.Length;
        
        if (bytesWritten != sendLength) throw new InvalidOperationException("invalid length");
            
        return new Memory<byte>(sendBuffer, 0, sendLength);
    }
}

/// subset of <see cref="DrawCommand"/>
[StructLayout(LayoutKind.Sequential)]
public struct WsDrawCommand
{
    public  Matrix4x4       projection;     // 64 bytes
    public  RectVector2     scissor;        // 16 bytes
    public  MemoryView      vertexView;     //  8 bytes
}