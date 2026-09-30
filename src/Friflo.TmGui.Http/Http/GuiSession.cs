// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.



using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Friflo.TmGui.Session;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;

public sealed class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    private  readonly   WsBackend       wsBackend;
    private  readonly   WsBatch         wsBatch;        // instance: creates / owns
    private             WsDrawCommand[] wsDrawList      = [];
    private             byte[]          sendBuffer      = [];
    private             int             canvasWidth     = 500;
    private             int             canvasHeight    = 300;
    
    public static GuiSession CreateGuiSession(TmClient client, IGuiAssets assets)
    {
        return new GuiSession(client, assets);
    }
    
    private GuiSession(TmClient client, IGuiAssets assets)
    {
        wsBackend   = new WsBackend(assets);
        wsBatch     = wsBackend.CreateBatch();
        this.client = client;
    }
    
    protected override TmGuiBackend    Backend => wsBackend;

    private Vector2     pendingMousePos;
    private bool        isMouseMoveEvent;
    
    public override void ProcessInput(ReadOnlySpan<byte> utf8Bytes)
    {
        Span<char> chars = stackalloc char[utf8Bytes.Length];
        int charCount = Encoding.UTF8.GetChars(utf8Bytes, chars);
        ReadOnlySpan<char> span = chars.Slice(0, charCount);

        // Max 16 key-value pairs on the stack
        Span<Range> ranges = stackalloc Range[16];
        int count = span.Split(ranges, ';', StringSplitOptions.RemoveEmptyEntries);

        isMouseMoveEvent = false;

        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<char> pair = span[ranges[i]];

            int eqIndex = pair.IndexOf('=');
            if (eqIndex < 0) continue;

            ReadOnlySpan<char> key = pair.Slice(0, eqIndex);
            ReadOnlySpan<char> value = pair.Slice(eqIndex + 1);

            DispatchParam(key, value);
        }

        // Dispatch aggregated MouseMove event
        if (isMouseMoveEvent)
        {
            wsBackend.AddEvent(new TmEvent(TmEventType.MouseMotion, pendingMousePos));
        }
    }

    private void DispatchParam(ReadOnlySpan<char> key, ReadOnlySpan<char> value)
    {
        if (key is "canvasWidth" && int.TryParse(value, out int width)) {
            canvasWidth = width;
        }
        else if (key is "canvasHeight" && int.TryParse(value, out int height)) {
            canvasHeight = height;
        }
        else if (key is "evt") {
            if (value is "mousemove") {
                isMouseMoveEvent = true;
            }
        }
        else if (key is "mouseX" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pendingMousePos.X)) {
        }
        else if (key is "mouseY" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pendingMousePos.Y)) {
        }
    }

    public override Memory<byte> IterateTui()
    {
        wsBackend.NewFrame();
        guiView!.RenderGui(wsBatch, canvasWidth, canvasHeight);
        
        wsBatch.DrawCommandList();

        var drawCommands = wsBatch.DrawList;

        if (wsDrawList.Length < drawCommands.Length) {
            wsDrawList = new WsDrawCommand [drawCommands.Length];
        }
        for (int n = 0; n < drawCommands.Length; n++)
        {
            var cmd = drawCommands[n];
            wsDrawList[n] = new WsDrawCommand {
                vertexView  = cmd.vertexView,
                projection  = cmd.projection,
                scissor     = cmd.scissor,
            };
        }
        var vertices = wsBatch.Vertices;
        
        var sendLength = 4 + drawCommands.Length * Unsafe.SizeOf<WsDrawCommand>() +
                         4 + vertices.Length     * Unsafe.SizeOf<Vertex2D>();
        if (sendBuffer.Length < sendLength) {
            sendBuffer = new byte[sendLength];
        }
        Span<byte> span = sendBuffer;
        int bytesWritten = 0;

        // 1. Write drawCommands.Count (int)
        MemoryMarshal.Write(span[bytesWritten..], drawCommands.Length);
        bytesWritten += sizeof(int);
        
        // 2. Write vertices.Length (int)
        MemoryMarshal.Write(span[bytesWritten..], vertices.Length);
        bytesWritten += sizeof(int);

        // 3. Write wsDrawList elements
        var drawListBytes = MemoryMarshal.AsBytes(wsDrawList.AsSpan(0, drawCommands.Length));
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