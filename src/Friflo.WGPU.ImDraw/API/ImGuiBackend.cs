// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public abstract class ImBuffer<T> : IDisposable where T : unmanaged 
{
    public abstract void        Dispose();
    public abstract Memory<T>   Memory { get; }
    public abstract void        Write(int start, int length);
}

public abstract class ImGuiBackend
{

    public    abstract  Font                CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name);
    public    abstract  Font                CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name);
    
    protected internal abstract  ImTexture           CreateTexture(int width, int height, ReadOnlySpan<byte> rgbaPixels);
    protected internal abstract  ImBuffer<Vertex2D>  CreateVertexBuffer(int vertexCount);
    protected internal abstract  ImBuffer<uint>      CreateIndexBuffer(int indexCount);
}