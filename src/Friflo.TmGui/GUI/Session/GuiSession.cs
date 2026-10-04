// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InlineTemporaryVariable
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;


internal sealed class GuiIterateBuffers
{
    internal            WsDrawCommand[] wsDrawList          = [];   // move to GuiIterateBuffers
    internal            byte[]          sendBuffer          = [];   // move to GuiIterateBuffers

    // --- changes
    internal readonly   List<SeqChange> changeList          = [];   // move to GuiIterateBuffers
    internal            int[]           quadHashesBuffer    = [];   // move to GuiIterateBuffers
    internal            VertexQuad[]    quadBuffer          = [];   // move to GuiIterateBuffers
}

public sealed partial class GuiSession : TmSession
{
    private  readonly   TmClient            client;             // instance: passed
    private  readonly   WsBackend           wsBackend;          // instance: creates / owns
    private  readonly   WsBatch             wsBatch;            // instance: creates / owns
    private  readonly   GuiIterateBuffers   buffers;            // instance: shared
    
    private  readonly   HashSet<int>        clientTextures      = [];
    private             ulong               lastSendBufferHash;
    private             int                 canvasWidth         = 500;
    private             int                 canvasHeight        = 300;
    // --- changes
    private readonly    List<int>           clientQuadList      = [];
    
    public static GuiSession CreateGuiSession(TmClient client, TmSessionLoop loop)
    {
        return new GuiSession(client, loop);
    }
    
    private GuiSession(TmClient client, TmSessionLoop loop)
    {
        buffers     = loop.iterateBuffers;
        wsBackend   = new WsBackend(loop.rootBackend);
        wsBatch     = wsBackend.CreateBatch();
        this.client = client;
    }
    
    protected internal override TmGuiBackend    Backend          => wsBackend;
    protected internal override bool            ReceivedInput    => receivedInput;


    internal override Memory<byte> IterateUI(AssetResources resources)
    {
        receivedInput = false;
        
        wsBackend.NewFrame();
        guiView!.RenderGui(wsBatch, canvasWidth, canvasHeight);
        
        wsBatch.DrawCommandList();

        var usedTextures    = clientTextures;
        var drawCommands    = wsBatch.DrawList;
        var newTextures     = wsBackend.newTextures;
        newTextures.Clear();

        if (buffers.wsDrawList.Length < drawCommands.Length) {
            buffers.wsDrawList = new WsDrawCommand [drawCommands.Length];
        }
        for (int n = 0; n < drawCommands.Length; n++)
        {
            var cmd = drawCommands[n];
            var textureId = resources.GetTexture(cmd.texture);
            if (usedTextures.Add(textureId)) {
                newTextures.Add(textureId);
            }
            buffers.wsDrawList[n] = new WsDrawCommand {
                vertexView  = cmd.vertexView,
                projection  = cmd.projection,
                scissor     = cmd.scissor,
                textureId   = textureId
            };
        }
        var vertices = wsBatch.Vertices;
        // vertices = CalcQuadChanges(vertices);
        
        var sendLength = 8 + 8 + 4 + 4 + 4 + 4 + 
                         drawCommands.Length        * Unsafe.SizeOf<WsDrawCommand>() +
                         vertices.Length            * Unsafe.SizeOf<Vertex2D>() +
                         buffers.changeList.Count   * Unsafe.SizeOf<SeqChange>();
        if (buffers.sendBuffer.Length < sendLength) {
            buffers.sendBuffer = new byte[sendLength + 1000]; // TODO 1000 ?
        }
        Span<byte> span = buffers.sendBuffer;
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
        MemoryMarshal.Write(span[bytesWritten..], buffers.changeList.Count);
        bytesWritten += sizeof(int);
        
        // 4. Write current mouse cursor shape (int)
        MemoryMarshal.Write(span[bytesWritten..], wsBackend.input.CurrentCursor);
        bytesWritten += sizeof(int);

        // 5. Write wsDrawList elements
        var drawListBytes = MemoryMarshal.AsBytes(buffers.wsDrawList.AsSpan(0, drawCommands.Length));
        drawListBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += drawListBytes.Length;

        // 6. Write vertices elements
        var vertexBytes = MemoryMarshal.AsBytes(vertices);
        vertexBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += vertexBytes.Length;
        
        // 7. Write change elements
        var changes = CollectionsMarshal.AsSpan(buffers.changeList);
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
        
        var memory = new Memory<byte>(buffers.sendBuffer, 0, bytesWritten);

        // remove time values from hash calculation
        var timeOffsets = 8 + 8;
        var sendHash = HashUtils.XxHash3(memory.Span.Slice(timeOffsets, memory.Length - timeOffsets));
        if (lastSendBufferHash == sendHash) {
            return default;
        }
        lastSendBufferHash = sendHash;
        return memory;
    }
}

/// subset of <see cref="DrawCommand"/>
[StructLayout(LayoutKind.Sequential)]
internal struct WsDrawCommand
{
    public  Matrix4x4       projection;     // 64 bytes
    public  RectVector2     scissor;        // 16 bytes
    public  MemoryView      vertexView;     //  8 bytes
    public  int             textureId;      //  4 bytes          
}