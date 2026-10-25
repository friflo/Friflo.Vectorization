// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.SmGui.Session;
using Friflo.SmGui.TUI;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable EmptyConstructor
// ReSharper disable RedundantOverriddenMember
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Headless;

public sealed class CpuBackend : SmGuiBackend
{
    private  readonly   SmGuiBackend    rootBackend;
    internal readonly   string          backendName;
    
    public   override   string          ToString()  => backendName;
    
    /// <summary>
    /// Use a <see cref="TuiAssets"/> instance for a pure TUI use.<br/>
    /// Drawing Sixel graphics in a TUI requires a <c>DefaultGuiAssets</c> instance from package <c>Friflo.SmGui.Assets</c>.
    /// </summary>
    public CpuBackend(string name, IGuiAssets assets) : base(assets) {
        backendName = name;
        rootBackend = null!;
    }
    
    public CpuBackend(string name, SmGuiBackend rootBackend) : base(rootBackend.Assets) {
        backendName         = name;
        this.rootBackend    = rootBackend;
    }
    
    public override void Dispose() {
        base.Dispose();
    }
    
    public CpuGuiBatch CreateGuiBatch(SessionId sessionId = default, int maxVertices = 60_000, LoopTimer? timer = null, UiType uiType = UiType.None) {
        var batch = new CpuGuiBatch(sessionId, this, maxVertices, timer) { UiType = uiType };
        InitBatch(batch);
        return batch;
    }
    
    public TuiBatch CreateTuiBatch(TuiColorMode colorMode, SessionId sessionId = default, LoopTimer? timer = null, UiType uiType = UiType.None)
    {
        var batch = new TuiBatch(sessionId, this, colorMode, timer) { UiType = uiType };
        InitBatch(batch);
        return batch;
    }
    
    protected internal override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        return new CpuMemoryBuffer<Vertex2D>(vertexCount);
    }

    protected internal override SmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        // index buffer if specific for GPU. CPU utilize only vertex buffer
        return new CpuMemoryBuffer<uint>();
    }
    
    
    public override SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        var array = rgbaPixels.ToArray();
        var length = width * height * 4;
        if (array.Length < length) {
            throw new InvalidOperationException($"texture array too small. Was: {array.Length}. Requires: {length} width: {width} height: {height}");
        }
        var native = new CpuTexture(name, width, height, array);
        return new SmTexture(native, 0);
    }
    
    public override SmTexture LoadTexture(Stream stream, string? label = null, SmTextureUsage usage = SmTextureUsage.TextureBinding | SmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);

        // texture.Write(image.data, bytesPerRow: image.width * 4, rowsPerImage: image.height);
        var cpuTexture = new CpuTexture(label!, image.width, image.height, image.data);
        return new SmTexture(cpuTexture, 0);
    }
    
    public override SmImageAsset GetTextureImage(SmTexture texture)
    {
        if (texture.native is CpuTexture tex) {
            return new SmImageAsset { width = tex.width, height = tex.height, data = tex.rgbaPixels, name = tex.name };
        }
        return rootBackend.GetTextureImage(texture);
    }
}
