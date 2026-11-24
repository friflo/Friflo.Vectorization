// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.SmGui.OpenTK;

public sealed class OpenTkGuiBackend : SmGuiBackend
{
    public OpenTkGuiBackend(IGuiAssets assets) : base(assets) { }

    public OpenTkBatch CreateBatch(int maxVertices = 60_000)
    {
        var batch = new OpenTkBatch(this, maxVertices) { UiType = UiType.NativeGui };
        InitBatch(batch);
        return batch;
    }

    public override SmTexture CreateTexture(string name, int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        int texId = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texId);
        GL.PixelStore(PixelStoreParameter.UnpackRowLength, width);

        var copy = rgbaPixels.ToArray();
        GL.TexImage2D(
            TextureTarget.Texture2D,
            0,
            PixelInternalFormat.Rgba,
            width,
            height,
            0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte,
            copy);

        GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        var data = new OpenTkTextureData(texId, width, height, name, copy);
        return data.AsSmTexture();
    }

    protected override SmBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        int vbo = GL.GenBuffer();
        var array = new Vertex2D[vertexCount];
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, array.Length * Marshal.SizeOf<Vertex2D>(), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        return new OpenTkBuffer<Vertex2D>(vbo, BufferTarget.ArrayBuffer, array);
    }

    protected override SmBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        int ebo = GL.GenBuffer();
        var array = new uint[indexCount];
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, array.Length * sizeof(uint), IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        return new OpenTkBuffer<uint>(ebo, BufferTarget.ElementArrayBuffer, array);
    }

    public override SmTexture LoadTexture(Stream stream, string label = null, SmTextureUsage usage = SmTextureUsage.TextureBinding | SmTextureUsage.CopyDst)
    {
        var image = assets.LoadImage(stream, SmColorComponents.RedGreenBlueAlpha);
        return CreateTexture(label, image.width, image.height, image.data);
    }

    public override SmImageAsset GetTextureImage(SmTexture texture)
    {
        var tex = (OpenTkTextureData)texture.native!;
        return new SmImageAsset { width = tex.Width, height = tex.Height, data = tex.Pixels, name = tex.Label };
    }
}