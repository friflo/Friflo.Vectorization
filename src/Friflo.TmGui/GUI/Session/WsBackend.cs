// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.TmGui.Session;

internal class WsBackend : TmGuiBackend
{
    private  readonly   SessionId       sessionId;
    private  readonly   TmGuiBackend    rootBackend;

    public   override   string          ToString() => $"session: {sessionId}";

    internal WsBackend(TmGuiBackend rootBackend, SessionId sessionId) : base(rootBackend.Assets)
    {
        this.sessionId      = sessionId;   
        this.rootBackend    = rootBackend;
    }
    
    public    override   TmFont      DefaultFont => rootBackend.DefaultFont;

    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new GuiBuffer<Vertex2D>(vertexCount);
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        // no index buffer used for remote Gui
        return new GuiBuffer<uint>(0);
    }

    internal const string Sid = "sid/";

    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        name = $"{Sid}{sessionId.str}/{name}";
        return rootBackend.CreateTexture(name, width, height, rgbaPixels);
    }

    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.None | TmTextureUsage.CopyDst | TmTextureUsage.TextureBinding)
    {
        label = $"{Sid}{sessionId.str}/{label}";
        return rootBackend.LoadTexture(stream, label, usage);
    }
    
    public override TmImageAsset GetTextureImage(TmTexture texture)
    {
        return rootBackend.GetTextureImage(texture);
    }
    
    internal WsBatch CreateBatch()  // WS_TAG
    {
        var batch = new WsBatch(this, 60000);
        InitBatch(batch);
        return batch;
    }
}

internal sealed class GuiBuffer<T> : TmBuffer<T> where T : unmanaged
{
    private readonly    Memory<T>   memory;
    
    public  override    Memory<T>   Memory => memory;

    public  override    string      ToString() => $"{typeof(T).Name}[{memory.Length}]";

    internal GuiBuffer(int length) {
        if (length == 0) return;
        memory = new Memory<T>(new T[length]);
    }
    
    public override void Dispose() {
    }
    
    public override void Write(int start, int length) {
        // no GPU involved => no copy
    }
}
