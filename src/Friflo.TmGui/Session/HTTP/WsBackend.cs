using System;
using System.IO;
using Friflo.TmGui.Headless;

// ReSharper disable ConvertToPrimaryConstructor
namespace Friflo.TmGui.Session.HTTP;

internal class WsBackend : TmGuiBackend
{
    public WsBackend(IGuiAssets assets) : base(assets)
    {
    }

    protected internal override TmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var buffer = new MemoryBuffer<Vertex2D>(vertexCount);
        return new HeadlessBuffer<Vertex2D>(buffer);
    }

    protected internal override TmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var buffer = new MemoryBuffer<uint>(indexCount);
        return new HeadlessBuffer<uint>(buffer);
    }

    public override TmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        throw new NotImplementedException();
    }

    public override TmTexture LoadTexture(Stream stream, string? label = null, TmTextureUsage usage = TmTextureUsage.None | TmTextureUsage.CopyDst | TmTextureUsage.TextureBinding)
    {
        throw new NotImplementedException();
    }
    
    public WsBatch CreateBatch()
    {
        var batch = new WsBatch(this);
        InitBatch(batch);
        return batch;
    }
}