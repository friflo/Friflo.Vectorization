// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.SmGui.Headless;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Session;

internal class GuiBackend : SmGuiBackend
{
    private  readonly   SessionKeys         keys;
    private  readonly   CpuBackend          cpuBackend;
    private  readonly   GuiSessionShared    shared;
    

    public   override   string          ToString() => $"session: {keys.id}";

    internal GuiBackend(CpuBackend cpuBackend, GuiSessionShared guiShared, SessionKeys keys) : base(cpuBackend.Assets)
    {
        this.keys           = keys;
        this.cpuBackend     = cpuBackend;
        shared              = guiShared;
    }
    
    public    override   SmFont      DefaultFont => cpuBackend.DefaultFont;

    protected internal override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var vertexBuffer = shared.vertexBuffer;
        if (vertexBuffer.Memory.Length < vertexCount) {
            vertexBuffer = shared.vertexBuffer = new GuiBuffer<Vertex2D>(vertexCount);
        }
        return vertexBuffer;
    }

    protected internal override SmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        // no index buffer used for remote Gui
        return new GuiBuffer<uint>(0);
    }

    internal const string Sid = "sid/";

    public override SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        name = $"{Sid}{keys.guid.str}/{name}";
        return cpuBackend.CreateTexture(name, width, height, rgbaPixels);
    }

    public override SmTexture LoadTexture(Stream stream, string? label = null, SmTextureUsage usage = SmTextureUsage.None | SmTextureUsage.CopyDst | SmTextureUsage.TextureBinding)
    {
        label = $"{Sid}{keys.guid.str}/{label}";
        return cpuBackend.LoadTexture(stream, label, usage);
    }
    
    public override SmImageAsset GetTextureImage(SmTexture texture)
    {
        return cpuBackend.GetTextureImage(texture);
    }
    
    internal GuiBatch CreateBatch(SessionId sessionId, LoopTimer timer, UiType uiType)  // WS_TAG
    {
        var batch = new GuiBatch(sessionId, this, 60000, timer) { UiType = uiType };
        InitBatch(batch);
        return batch;
    }
}

internal sealed class GuiBuffer<T> : SmBuffer<T> where T : unmanaged
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
