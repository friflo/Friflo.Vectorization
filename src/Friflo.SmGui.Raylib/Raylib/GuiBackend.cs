// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Raylib_cs;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.Raylib;

public sealed class RaylibGuiBackend : SmGuiBackend
{
    // Reusable staging buffer to avoid per-call pixel array allocations in GetTextureImage.
    private byte[] readbackBuffer = Array.Empty<byte>();

    public RaylibGuiBackend(IGuiAssets assets) : base(assets) {
    }

    public RaylibBatch CreateBatch(int maxVertices = 60_000) {
        var batch = new RaylibBatch(this, maxVertices) { UiType = UiType.NativeGui };
        InitBatch(batch);
        return batch;
    }

    public override unsafe SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels) {
        fixed (byte* ptr = rgbaPixels) {
            // Upload RGBA8 pixels directly via rlgl without requiring Raylib.LoadTextureFromImage.
            uint id = Rlgl.LoadTexture(ptr, width, height, PixelFormat.UncompressedR8G8B8A8, 1);
            var texture = new Texture2D {
                Id      = id,
                Width   = width,
                Height  = height,
                Mipmaps = 1,
                Format  = PixelFormat.UncompressedR8G8B8A8
            };
            var raylibTexture = new RaylibTexture(texture, rgbaPixels.ToArray(), name);
            return raylibTexture.AsSmTexture();
        }
    }

    protected override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount) {
        return new RaylibVertexBuffer(vertexCount);
    }

    protected override SmBuffer<uint> CreateIndexBuffer(int indexCount) {
        return new RaylibIndexBuffer(indexCount);
    }

    public override SmTexture LoadTexture(Stream stream, string label = null, SmTextureUsage usage = SmTextureUsage.TextureBinding | SmTextureUsage.CopyDst) {
        var imageAsset = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);
        return CreateTexture(label!, imageAsset.width, imageAsset.height, imageAsset.data);
    }

    public override SmImageAsset GetTextureImage(SmTexture texture) {
        var rayTexture = texture.AsGpuTexture();
        int byteCount = rayTexture.native.Width * rayTexture.native.Height * 4;

        // Reuse staging buffer across calls when size is sufficient.
        if (readbackBuffer.Length < byteCount) {
            readbackBuffer = new byte[byteCount];
        }
        return new SmImageAsset {
            width   = rayTexture.native.Width,
            height  = rayTexture.native.Height,
            data    = rayTexture.pixels,
            name    = rayTexture.name
        };
    }
}