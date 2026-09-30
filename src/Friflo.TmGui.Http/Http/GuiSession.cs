// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Friflo.TmGui.Session;

// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public sealed partial class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    private  readonly   WsBackend       wsBackend;
    private  readonly   WsBatch         wsBatch;        // instance: creates / owns
    private             WsDrawCommand[] wsDrawList      = [];
    private             byte[]          sendBuffer      = [];
    private             ulong           lastSendBufferHash;
    private             int             canvasWidth     = 500;
    private             int             canvasHeight    = 300;
    
    public static GuiSession CreateGuiSession(TmClient client, TmGuiBackend rootBackend)
    {
        return new GuiSession(client, rootBackend);
    }
    
    private GuiSession(TmClient client, TmGuiBackend rootBackend)
    {
        wsBackend   = new WsBackend(rootBackend);
        wsBatch     = wsBackend.CreateBatch();
        this.client = client;
    }
    
    protected override TmGuiBackend    Backend => wsBackend;


    public override Memory<byte> IterateTui()
    {
        wsBackend.NewFrame();
        guiView!.RenderGui(wsBatch, canvasWidth, canvasHeight);
        
        wsBatch.DrawCommandList();

        var drawCommands = wsBatch.DrawList;
        var texture2Id   = wsBackend.texture2Id;
        var usedTextures   = wsBackend.usedTextures;
        usedTextures.Clear();

        if (wsDrawList.Length < drawCommands.Length) {
            wsDrawList = new WsDrawCommand [drawCommands.Length];
        }
        for (int n = 0; n < drawCommands.Length; n++)
        {
            var cmd = drawCommands[n];
            if (!texture2Id.TryGetValue(cmd.texture, out int textureId)) {
                textureId = wsBackend.AddTexture(cmd.texture);
            }
            usedTextures.Add(textureId);
            wsDrawList[n] = new WsDrawCommand {
                vertexView  = cmd.vertexView,
                projection  = cmd.projection,
                scissor     = cmd.scissor,
                textureId   = textureId
            };
        }
        var vertices = wsBatch.Vertices;
        
        var sendLength = 4 + 4 + 4 + 
                         drawCommands.Length * Unsafe.SizeOf<WsDrawCommand>() +
                         vertices.Length     * Unsafe.SizeOf<Vertex2D>();
        if (sendBuffer.Length < sendLength) {
            sendBuffer = new byte[sendLength + 1000];
        }
        Span<byte> span = sendBuffer;
        int bytesWritten = 0;

        // 1. Write drawCommands.Count (int)
        MemoryMarshal.Write(span[bytesWritten..], drawCommands.Length);
        bytesWritten += sizeof(int);
        
        // 2. Write vertices.Length (int)
        MemoryMarshal.Write(span[bytesWritten..], vertices.Length);
        bytesWritten += sizeof(int);
        
        // 3. Write current mouse cursor shape (int)
        MemoryMarshal.Write(span[bytesWritten..], wsBackend.input.CurrentCursor);
        bytesWritten += sizeof(int);

        // 4. Write wsDrawList elements
        var drawListBytes = MemoryMarshal.AsBytes(wsDrawList.AsSpan(0, drawCommands.Length));
        drawListBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += drawListBytes.Length;

        // 5. Write vertices elements
        var vertexBytes = MemoryMarshal.AsBytes(vertices);
        vertexBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += vertexBytes.Length;
        
        if (bytesWritten != sendLength) throw new InvalidOperationException("invalid length");
        
        // 6. Write used textures
        var images = wsBackend.images;
        MemoryMarshal.Write(span[bytesWritten..], usedTextures.Count);
        bytesWritten += sizeof(int);
        foreach (var usedTexture in usedTextures) {
            var image = images[usedTexture];
            MemoryMarshal.Write(span[bytesWritten..], image.textureId);
            bytesWritten += sizeof(int);
            
            int utf8ByteCount = Encoding.UTF8.GetByteCount(image.asset.name);
            MemoryMarshal.Write(span[bytesWritten..], utf8ByteCount);
            bytesWritten += sizeof(int);
            int encodedBytes = Encoding.UTF8.GetBytes(image.asset.name, span[bytesWritten..]);
            bytesWritten += encodedBytes;
        }
        
        var memory = new Memory<byte>(sendBuffer, 0, sendLength);
        
        var sendHash = HashUtils.XxHash3(memory.Span);
        if (lastSendBufferHash == sendHash) {
            return default;
        }
        lastSendBufferHash = sendHash;
        return memory;
    }
}

/// subset of <see cref="DrawCommand"/>
[StructLayout(LayoutKind.Sequential)]
public struct WsDrawCommand
{
    public  Matrix4x4       projection;     // 64 bytes
    public  RectVector2     scissor;        // 16 bytes
    public  MemoryView      vertexView;     //  8 bytes
    public  int             textureId;      //  4 bytes          
}