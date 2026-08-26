// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Friflo.GPU;


// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;

public sealed class WgpuGuiBackend : ImGuiBackend, IDisposable  
{
    private readonly GpuDevice device;
    
    public WgpuGuiBackend(GpuDevice device) {
        this.device = device;
    }
    
    public void Dispose() {
    }
        
    public override Font CreateBMFont(ReadOnlySpan<char> fntContent, Stream fontAtlas, string name)
    {
        throw new NotImplementedException();
    }

    public override Font CreateTtfFont(Stream ttfStream, float fontSize, int width, int height, int firstChar, int charCount, string name)
    {
        throw new NotImplementedException();
    }

    protected internal override ImTexture CreateTexture(int width, int height, ReadOnlySpan<byte> rgbaPixels)
    {
        throw new NotImplementedException();
    }

    protected internal override ImBuffer<Vertex2D> CreateVertexBuffer(int vertexCount)
    {
        var vertices = new Memory<Vertex2D>(new Vertex2D[vertexCount]);
        var buffer   = device.CreateBuffer(vertices, "Batch2D Vertices", BufferProfile.StaticIn, BufferType.Vertex);
        return new ImWgpuBuffer<Vertex2D>(buffer);
    }

    protected internal override ImBuffer<uint> CreateIndexBuffer(int indexCount)
    {
        var indices = new Memory<uint>(new uint[indexCount]);
        var buffer  = device.CreateBuffer(indices, "Batch2D Indices", BufferProfile.StaticIn, BufferType.Index);
        return new ImWgpuBuffer<uint>(buffer);
    }
}

internal class ImWgpuBuffer<T> : ImBuffer<T> where T : unmanaged
{
    internal readonly GpuBuffer<T>  native;
    public   override Memory<T>     Memory => native.hostMemory;
    
    public ImWgpuBuffer(GpuBuffer<T> buffer) {
        native = buffer;
    }

    public override void Dispose() {
        native.Dispose();
    }
    
    public override void Write(int start, int length) {
        native.In(start, length).Write();
    }
}