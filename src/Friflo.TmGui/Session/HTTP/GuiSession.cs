// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


// ReSharper disable CheckNamespace

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Friflo.TmGui.Session.HTTP;

internal sealed class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    internal            IGuiView?       guiView;
    internal readonly   WsBackend       wsBackend;
    private  readonly   WsBatch         wsBatch;        // instance: creates / owns
    private             WsDrawCommand[] wsDrawList = [];
    private             byte[]          sendBuffer = [];
    
    
    internal GuiSession(TmClient client, IGuiAssets assets)
    {
        wsBackend   = new WsBackend(assets);
        wsBatch     = wsBackend.CreateBatch();
        this.client = client;
    }

    public override void ProcessInput(ReadOnlySpan<byte> input)
    {
        throw new NotImplementedException("*********** TEST");
    }

    public override Memory<byte> IterateTui()
    {
        guiView!.RenderGui(wsBatch, 500, 300);

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