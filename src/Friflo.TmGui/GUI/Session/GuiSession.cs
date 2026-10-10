// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable ConvertToConstant.Local
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable InlineTemporaryVariable
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;



internal readonly struct SessionId
{
    internal readonly   Guid    value;
    internal readonly   string? str;

    public   override   string  ToString() => str ?? "null";
    
    internal SessionId(long id)
    {
        value   = Guid.Parse($"{id:D32}");
        str     = id.ToString();
    }
   
    private SessionId(Guid guid) {
        value   = guid;
        str     = value.ToString();
    }
    
    internal static SessionId FromSpan(ReadOnlySpan<char> sidSpan)
    {
        if (long.TryParse(sidSpan, out var sidInt)) {
            return new SessionId(sidInt);
        }
        if (Guid.TryParse(sidSpan, out Guid guid)) {
            return new SessionId(guid);
        }
        return default;
    }
}

internal sealed class GuiSessionShared
{
    internal            WsDrawCommand[] wsDrawList          = [];
    internal            byte[]          sendBuffer          = [];
    internal readonly   List<int>       newTextures         = [];

    // --- changes
    internal readonly   List<SeqChange> changeList          = [];
    internal            int[]           quadHashesBuffer    = [];
    internal            VertexQuad[]    quadBuffer          = [];
}


internal sealed partial class GuiSession : TmSession
{
    private  readonly   WsBackend           wsBackend;          // instance: creates / owns
    internal readonly   WsBatch             wsBatch;            // instance: creates / owns
    private  readonly   GuiSessionShared    shared;             // instance: shared
    
    private  readonly   HashSet<int>        clientTextures      = [];
    private             ulong               lastSendBufferHash;
    private             int                 canvasWidth         = 500;
    private             int                 canvasHeight        = 300;
    // --- changes
    private readonly    List<int>           clientQuadList      = [];
    private readonly    bool                sendDiffs           = true;

    
    internal GuiSession(SessionId sessionId, TmSessionLoop loop, FrameTimer frameTimer)
        : base(sessionId)
    {
        shared              = loop.guiShared;
        wsBackend           = new WsBackend(loop.rootBackend, sessionId);
        wsBatch             = wsBackend.CreateBatch();
        wsBatch.frameTimer  = frameTimer;
        
        debugClientQuads = null!;
        DebugInit();
    }
    
    protected internal override TmGuiBackend    Backend => wsBackend;
    protected internal override TmBatch         Batch   => wsBatch;


    internal override Memory<byte> IterateUI(in AssetResources resources)
    {
        wsBackend.NewFrame();
        guiView!.RenderGui(wsBatch, canvasWidth, canvasHeight);
        
        wsBatch.DrawCommandList();

        var usedTextures    = clientTextures;
        var drawCommands    = wsBatch.DrawList;
        var newTextures     = shared.newTextures;
        newTextures.Clear();

        if (shared.wsDrawList.Length < drawCommands.Length) {
            shared.wsDrawList = new WsDrawCommand [drawCommands.Length];
        }
        var wsDrawList = shared.wsDrawList.AsSpan(0, drawCommands.Length);
        
        for (int n = 0; n < drawCommands.Length; n++)
        {
            ref readonly var cmd = ref drawCommands[n];
            var textureId = resources.GetTexture(cmd.texture);
            if (textureId != 0 && usedTextures.Add(textureId)) {
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
        // case sendDiffs == false: -1     case sendDiffs == true: vertex count of full frame
        var diffVertexCount =  -1;
        if (sendDiffs) {
            diffVertexCount = vertices.Length;
            vertices = CalcVerticesDiff(vertices);
        }
        var changes = CollectionsMarshal.AsSpan(shared.changeList);
        
        var sendLength = 4 + 8 + 8 + 4 + 4 + 4 + 4 + 4 +
                         drawCommands.Length    * Unsafe.SizeOf<WsDrawCommand>() +
                         vertices.Length        * Unsafe.SizeOf<Vertex2D>() +
                         changes.Length         * Unsafe.SizeOf<SeqChange>();
        if (shared.sendBuffer.Length < sendLength) {
            shared.sendBuffer = new byte[sendLength + 1000]; // TODO 1000 ?
        }
        Span<byte> span = shared.sendBuffer;
        int bytesWritten = 0;
        
        // 0. Write RTT start time & send time (2 x double)
        MemoryMarshal.Write(span[bytesWritten..], -1);
        bytesWritten += sizeof(int);
        
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
        MemoryMarshal.Write(span[bytesWritten..], changes.Length);
        bytesWritten += sizeof(int);
        
        MemoryMarshal.Write(span[bytesWritten..], diffVertexCount);
        bytesWritten += sizeof(int);
        
        int hashStart = bytesWritten;
        
        // 4. Write current mouse cursor shape (int)
        MemoryMarshal.Write(span[bytesWritten..], wsBackend.input.CurrentCursor);
        bytesWritten += sizeof(int);
        
        // 5. Write used textures
        var texturesStart = bytesWritten;
        var images = resources.images;
        MemoryMarshal.Write(span[bytesWritten..], newTextures.Count);
        bytesWritten += sizeof(int);
        foreach (var usedTexture in newTextures) {
            var image = images[usedTexture];
            MemoryMarshal.Write(span[bytesWritten..], image!.textureId);
            bytesWritten += sizeof(int);
            
            int utf8ByteCount = Encoding.UTF8.GetByteCount(image.asset.name);
            MemoryMarshal.Write(span[bytesWritten..], utf8ByteCount);
            bytesWritten += sizeof(int);
            int encodedBytes = Encoding.UTF8.GetBytes(image.asset.name, span[bytesWritten..]);
            bytesWritten += encodedBytes;
        }
        var texturesLength = bytesWritten - texturesStart;

        // 6. Write wsDrawList elements
        var drawListBytes = MemoryMarshal.AsBytes(wsDrawList);
        drawListBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += drawListBytes.Length;

        // 7. Write vertices elements
        int vertexStart = bytesWritten;
        var vertexBytes = MemoryMarshal.AsBytes(vertices);
        vertexBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += vertexBytes.Length;
        
        // 8. Write change elements
        var changesBytes = MemoryMarshal.AsBytes(changes);
        changesBytes.CopyTo(span[bytesWritten..]);
        bytesWritten += changesBytes.Length;
        
        if (bytesWritten - texturesLength != sendLength) throw new InvalidOperationException("invalid length");

        // write terminator to check message consistency on client
        MemoryMarshal.Write(span[bytesWritten..], 0x12345678);
        bytesWritten += sizeof(int);
        
        MemoryMarshal.Write(span[..], bytesWritten);
        
        var memory = new Memory<byte>(shared.sendBuffer, 0, bytesWritten);

        if (sendDiffs) {
            // Debug.WriteLine($"----------- {sendCounter++}");
            // foreach (var change in shared.changeList) { Debug.WriteLine(change.ToString()); }
            var sendHash = HashUtils.XxHash3(memory.Span.Slice(hashStart, vertexStart - hashStart));
            var sendDiff = lastSendBufferHash != sendHash || changes.Length > 0;
            lastSendBufferHash = sendHash;
            if (sendDiff) {
                return memory;
            }
            return default;
        } else {
            var sendHash = HashUtils.XxHash3(memory.Span.Slice(hashStart, memory.Length - hashStart));
            if (lastSendBufferHash == sendHash) {
                return default;
            }
            lastSendBufferHash = sendHash;
            return memory;
        }
    }
}

/// subset of <see cref="DrawCommand"/>
[StructLayout(LayoutKind.Sequential)]
internal struct WsDrawCommand
{
    internal    Matrix4x4       projection;     // 64 bytes
    internal    RectVector2     scissor;        // 16 bytes
    internal    MemoryView      vertexView;     //  8 bytes
    internal    int             textureId;      //  4 bytes

    public override string ToString() => $"{vertexView}    tex: {textureId}    scissor: {scissor}";
}