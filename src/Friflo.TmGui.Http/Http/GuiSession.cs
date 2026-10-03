// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Friflo.TmGui.Session;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InlineTemporaryVariable
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Http;


public sealed partial class GuiSession : TmSession
{
    private  readonly   TmClient        client;         // instance: passed
    private  readonly   WsBackend       wsBackend;
    private  readonly   WsBatch         wsBatch;        // instance: creates / owns
    private  readonly   HashSet<int>    usedTexturesMap = [];
    private             WsDrawCommand[] wsDrawList      = [];
    private             byte[]          sendBuffer      = [];
    private             ulong           lastSendBufferHash;
    private             int             canvasWidth     = 500;
    private             int             canvasHeight    = 300;
    // --- changes
    private readonly    List<SeqChange> changeList          = [];
    private             int[]           quadHashesBuffer    = [];
    private             int             targetQuadCount;
    private             int[]           targetQuadBuffer    = [];
    private             VertexQuad[]    quadBuffer          = [];
    
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
    
    protected override TmGuiBackend    Backend          => wsBackend;
    protected override bool            ReceivedInput    => receivedInput;


    public override Memory<byte> IterateUI(AssetResources resources)
    {
        receivedInput = false;
        
        wsBackend.NewFrame();
        guiView!.RenderGui(wsBatch, canvasWidth, canvasHeight);
        
        wsBatch.DrawCommandList();

        var usedTextures    = usedTexturesMap;
        var drawCommands    = wsBatch.DrawList;
        var newTextures     = wsBackend.newTextures;
        newTextures.Clear();

        if (wsDrawList.Length < drawCommands.Length) {
            wsDrawList = new WsDrawCommand [drawCommands.Length];
        }
        for (int n = 0; n < drawCommands.Length; n++)
        {
            var cmd = drawCommands[n];
            var textureId = resources.GetTexture(cmd.texture);
            if (usedTextures.Add(textureId)) {
                newTextures.Add(textureId);
            }
            wsDrawList[n] = new WsDrawCommand {
                vertexView  = cmd.vertexView,
                projection  = cmd.projection,
                scissor     = cmd.scissor,
                textureId   = textureId
            };
        }
        var vertices = wsBatch.Vertices;
        vertices = CalcQuadChanges(vertices);
        
        var sendLength = 8 + 8 + 4 + 4 + 4 + 4 + 
                         drawCommands.Length * Unsafe.SizeOf<WsDrawCommand>() +
                         vertices.Length     * Unsafe.SizeOf<Vertex2D>() +
                         changeList.Count    * Unsafe.SizeOf<SeqChange>();
        if (sendBuffer.Length < sendLength) {
            sendBuffer = new byte[sendLength + 1000]; // TODO 1000 ?
        }
        Span<byte> span = sendBuffer;
        int bytesWritten = 0;
        
        // 0. Write RTT start time & send time (2 x double)
        MemoryMarshal.Write(span[bytesWritten..], (double)rttStart);
        bytesWritten += sizeof(double);
        rttStart = 0;

        var sendTime = GetCurrentUnixNanoseconds();
        MemoryMarshal.Write(span[bytesWritten..], sendTime);
        bytesWritten += sizeof(double);

        // 1. Write drawCommands.Count (int)
        MemoryMarshal.Write(span[bytesWritten..], drawCommands.Length);
        bytesWritten += sizeof(int);
        
        // 2. Write vertices.Length (int)
        MemoryMarshal.Write(span[bytesWritten..], vertices.Length);
        bytesWritten += sizeof(int);
        
        // 3. Write change count (int)
        MemoryMarshal.Write(span[bytesWritten..], changeList.Count);
        bytesWritten += sizeof(int);
        
        // 4. Write current mouse cursor shape (int)
        MemoryMarshal.Write(span[bytesWritten..], wsBackend.input.CurrentCursor);
        bytesWritten += sizeof(int);

        // 5. Write wsDrawList elements
        var drawListBytes = MemoryMarshal.AsBytes(wsDrawList.AsSpan(0, drawCommands.Length));
        drawListBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += drawListBytes.Length;

        // 6. Write vertices elements
        var vertexBytes = MemoryMarshal.AsBytes(vertices);
        vertexBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += vertexBytes.Length;
        
        // 7. Write change elements
        var changes = CollectionsMarshal.AsSpan(changeList);
        var changesBytes = MemoryMarshal.AsBytes(changes);
        changesBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += changesBytes.Length;
        
        if (bytesWritten != sendLength) throw new InvalidOperationException("invalid length");
        
        // 8. Write used textures   
        var images = resources.images;
        MemoryMarshal.Write(span[bytesWritten..], newTextures.Count);
        bytesWritten += sizeof(int);
        foreach (var usedTexture in newTextures) {
            var image = images[usedTexture];
            MemoryMarshal.Write(span[bytesWritten..], image.textureId);
            bytesWritten += sizeof(int);
            
            int utf8ByteCount = Encoding.UTF8.GetByteCount(image.asset.name);
            MemoryMarshal.Write(span[bytesWritten..], utf8ByteCount);
            bytesWritten += sizeof(int);
            int encodedBytes = Encoding.UTF8.GetBytes(image.asset.name, span[bytesWritten..]);
            bytesWritten += encodedBytes;
        }
        
        var memory = new Memory<byte>(sendBuffer, 0, bytesWritten);

        // remove time values from hash calculation
        var timeOffsets = 8 + 8;
        var sendHash = HashUtils.XxHash3(memory.Span.Slice(timeOffsets, memory.Length - timeOffsets));
        if (lastSendBufferHash == sendHash) {
            return default;
        }
        lastSendBufferHash = sendHash;
        return memory;
    }
    
    private Span<Vertex2D> CalcQuadChanges(Span<Vertex2D> vertices)
    {
        Span<VertexQuad> quads = MemoryMarshal.Cast<Vertex2D, VertexQuad>(vertices);
        if (quadHashesBuffer.Length < quads.Length) {
            quadHashesBuffer = new int[quads.Length];
        }
        var quadHashes = quadHashesBuffer.AsSpan(0, quads.Length);
        for (int n= 0; n < quads.Length; n++) {
            quadHashes[n] = quads[n].GetHashCode();
        }
        var targetQuads = targetQuadBuffer.AsSpan(0, targetQuadCount);
        SequenceDiff.TryComputeChanges(quadHashes, targetQuads, 100000, changeList, out int diffItemCount);
        if (targetQuadBuffer.Length < quadHashes.Length) {
            targetQuadBuffer = new int[quadHashes.Length];
        }
        quadHashes.CopyTo(targetQuadBuffer);
        targetQuadCount = quadHashes.Length;
        
        /* if (changeList.Count == 0) {
            return vertices;
        } */
        return vertices;
        if (quadBuffer.Length < diffItemCount) {
            quadBuffer = new VertexQuad[diffItemCount];
        }
        var diffQuads = quadBuffer.AsSpan(0, diffItemCount);
        var targetPos = 0;
        foreach (var change in changeList) {
            switch (change.Type) {
                case SeqChangeType.Insert:
                case SeqChangeType.Modify:
                    var target = diffQuads.Slice(targetPos, change.Length);
                    quads.Slice(change.Start, change.Length).CopyTo(target);
                    targetPos  += change.Length;
                    break;
            }
        }
        Debug.Assert(targetPos == diffItemCount);
        return vertices;
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