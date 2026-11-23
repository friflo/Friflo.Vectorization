// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// file: Batch.cs

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using GdColor   = Godot.Color;
using GdVector2 = Godot.Vector2;
using Vector2   = System.Numerics.Vector2;

// ReSharper disable ReplaceSliceWithRangeIndexer
// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable CheckNamespace
namespace Friflo.SmGui.Godot;

public sealed class GodotBatch : SmBatch
{
    internal GodotBatch(GodotGuiBackend backend, int maxVertices)
        : base(default, backend, maxVertices, null)
    {
        // samplerLinear   = backend.samplerLinear;
        // samplerNearest  = backend.samplerNearest;
    }

    protected override void InitBatch() { }
    
    // Godot clips per canvas item (not per draw call like a GPU scissor rect).
    // So every draw command is added to its own child canvas item. The items are reused every frame.
    // Child items are drawn in creation order => item[i] is drawn before item[i + 1].
    private readonly    List<Rid>   commandItems = [];
    private             Rid         parentItem;
    private             GdVector2[] points      = [];
    private             GdVector2[] uvs         = [];
    private             GdColor[]   colors      = [];
    
    /// <c>canvasItem</c> is the parent canvas item the GUI is drawn into. E.g. <see cref="CanvasItem.GetCanvasItem"/>
    public void DrawCommandList(Rid canvasItem, int x, int y, int width, int height)
    {
        EndBatch();
        
        if (parentItem != canvasItem) {
            parentItem = canvasItem;
            foreach (var item in commandItems) {
                RenderingServer.CanvasItemSetParent(item, canvasItem);
            }
        }
        
        // Godot canvas coordinates are already in pixels => cmd.projection is not required.
        // The viewport (x, y, width, height) maps to the item transform.
        var scaleX      = viewport.X > 0 ? width  / viewport.X : 1f;
        var scaleY      = viewport.Y > 0 ? height / viewport.Y : 1f;
        var transform   = new Transform2D(new GdVector2(scaleX, 0), new GdVector2(0, scaleY), new GdVector2(x, y));
        
        var scissor = new RectVector2(Vector2.Zero, viewport);

        // --- cast indexData and copy vertexData
        var vertexData  = ((GodotVertexBuffer)gpuVertexBuffer).vertexData;
        var indexData   = ((GodotIndexBuffer) gpuIndexBuffer).indexData;
        
        ReadOnlySpan<int> indices = MemoryMarshal.Cast<uint, int>(indexData);
        
        var vertexCount = Vertices.Length;
        if (points.Length < vertexCount) {
            points  = new GdVector2[vertexCount];
            uvs     = new GdVector2[vertexCount];
            colors  = new GdColor  [vertexCount];
        }
        for (int n = 0; n < vertexCount; n++) {
            ref readonly var vertex = ref vertexData[n];
            points[n] = new GdVector2(vertex.position.X, vertex.position.Y);
            uvs[n]    = new GdVector2(vertex.uv.X,       vertex.uv.Y);
            colors[n] = ToColor(vertex.color);
        }
        var itemCount = 0;
        foreach (var cmd in DrawList)
        {
            var item        = GetCommandItem(canvasItem, itemCount++);
            var texture     = ((Texture2D)cmd.texture.native!).GetRid();

            // var sampler  = cmd.samplerFilter == SamplerFilter.Linear ? samplerLinear : samplerNearest;   // todo  implement cmd.samplerFilter
            // var config   = renderConfigs[(int)cmd.blendState];                                           // todo  implement cmd.blendState
            
            RenderingServer.CanvasItemClear(item);
            RenderingServer.CanvasItemSetTransform(item, transform);
            
            var clip = !cmd.scissor.Equals(scissor);
            RenderingServer.CanvasItemSetClip(item, clip);
            if (clip) {
                var pos  = cmd.scissor.pos;
                var size = cmd.scissor.size;
                RenderingServer.CanvasItemSetCustomRect(item, true, new Rect2(pos.X, pos.Y, size.X, size.Y));
            } else {
                RenderingServer.CanvasItemSetCustomRect(item, false);
            }
            var indexSpan = indices.Slice(cmd.indexView.offset);
            RenderingServer.CanvasItemAddTriangleArray(item, indexSpan, points, colors, uvs, default, default, texture, indices.Length / 3);
        }
        // clear items of previous frame not used by this frame
        for (int n = itemCount; n < commandItems.Count; n++) {
            RenderingServer.CanvasItemClear(commandItems[n]);
        }
    }
    
    private Rid GetCommandItem(Rid parent, int index)
    {
        if (index < commandItems.Count) {
            return commandItems[index];
        }
        var item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, parent);
        commandItems.Add(item);
        return item;
    }
    
    /// <summary>Call when the batch is no longer used.</summary>
    public void FreeCanvasItems()
    {
        foreach (var item in commandItems) {
            RenderingServer.FreeRid(item);
        }
        commandItems.Clear();
    }
    
    /// <summary>
    /// rgbaPacked is a packed RGBA8 (R in the lowest byte) - same layout as <see cref="Vertex2D.color"/> 
    /// </summary>
    private static GdColor ToColor(uint rgbaPacked)
    {
        const float scale = 1f / 255f;
        return new GdColor(
            ( rgbaPacked         & 0xff) * scale,
            ((rgbaPacked >>  8)  & 0xff) * scale,
            ((rgbaPacked >> 16)  & 0xff) * scale,
            ( rgbaPacked >> 24         ) * scale
        );
    }
}