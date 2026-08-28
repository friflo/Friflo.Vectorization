// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

// ReSharper disable InconsistentNaming
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;


public enum BlendState
{
    /** Standard transparent (default) */               Alpha,  
    /** Overwrites pixels completely (no blending) */   Opaque,
    /** Glow, light, particles (SrcAlpha + One) */      Additive,
    /** Shadows, tinting (Zero + Src) */                Multiply,
    /** Add colors directly */                          AddColors,
    /** Subtract colors directly */                     SubtractColors
}

public enum SamplerFilter
{
    /**  Hard/Pixelated edges (Pixel Art). */                   Nearest,
    /**  Smooth/Blended edges (Scales, High-Res Sprites). */    Linear
}


public abstract class ImBatch : IDisposable
{
#region public
    public readonly     ImGuiBackend        backend;
    public readonly     GuiInput            input;
    public ReadOnlySpan<DrawCommand>        DrawList        => new(drawList, 0, drawCommands.Count);
    public ReadOnlySpan<Vertex2D>           Vertices        => vertexBuffer.Span.Slice(0, vertexCount);
#endregion

#region protected
    protected readonly  ImBuffer<Vertex2D>  gpuVertexBuffer;
    protected readonly  ImBuffer<uint>      gpuIndexBuffer;
    protected internal  Vector2             viewport;
#endregion

#region private / internal
    private             DrawCommand[]       drawList        = [];
    private   readonly  List<DrawCommand>   drawCommands 	= [];
    private   readonly  List<CmdSegment>    commandSegments = [];
    internal  readonly  Memory<Vertex2D>    vertexBuffer;
    
    internal  readonly  Stack<RectVector2>  scissorStack        = [];
    internal  readonly  Stack<Matrix4x4>    transformStack      = [];
    internal  readonly  Stack<int>          zIndexStack         = [];
    internal  readonly  Stack<SamplerFilter>samplerFilterStack  = [];
    private   readonly  StringBuilder       stringBuilder       = new(512,512); // => first chunk: 512 chars
    internal  readonly  GuiState            guiState            = new();

    // --- resources owned by DrawModule
    internal readonly   GuiHost             host;
    internal            ImFont              defaultFont;
    internal            ImTexture           defaultFontTexture;
    
    // --- ImDraw - state
    internal            IFormatProvider     formatProvider;
    internal            Matrix4x4           defaultOrtho;
    internal            Matrix4x4           currentTransform;
    internal            BlendState          currentBlendState;
    internal            SamplerFilter       currentSamplerFilter;
    internal            RectVector2         currentScissor;
    internal            bool                sortZIndex;
    internal            int                 currentZIndex;
    private             int                 currentSequence;
    internal            Matrix4x4           projection;
    private             int                 vertexStart;                // start of next Draw()
    internal            int                 vertexCount;
    internal            ImTexture           currentTexture;

    
    protected ImBatch(ImGuiBackend backend, int maxVertices)
    {
        this.backend    = backend;
        
        formatProvider  = CultureInfo.InvariantCulture;
        host            = backend.host;
        input           = backend.input;
        
        // --- vertex & index buffer - to draw quads
        int maxQuads   = maxVertices / 4;
        int maxIndices = maxQuads * 6;

        gpuVertexBuffer = backend.CreateVertexBuffer(maxVertices);
        vertexBuffer    = gpuVertexBuffer.Memory;

        // generate quad indexes only once
        gpuIndexBuffer = backend.CreateIndexBuffer(maxIndices);
        var indices =  gpuIndexBuffer.Memory.Span;
        for (int i = 0, v = 0; i < maxIndices; i += 6, v += 4)
        {
            indices[i + 0] = (uint)(v + 0);
            indices[i + 1] = (uint)(v + 1);
            indices[i + 2] = (uint)(v + 2);
            indices[i + 3] = (uint)(v + 2);
            indices[i + 4] = (uint)(v + 3);
            indices[i + 5] = (uint)(v + 0);
        }
        gpuIndexBuffer.Write(0, maxIndices);
        
        defaultFont             = backend.DefaultFont;
        defaultFontTexture      = backend.DefaultFont.texture;
    }
    
    internal StringBuilder StringBuilder()
    {
        stringBuilder.Clear();
        return stringBuilder;
    }
#endregion


#region public
    public void Dispose()
    {
        gpuVertexBuffer.Dispose();
        gpuIndexBuffer.Dispose();
    }
    
    public void SetFont(ImFont font) {
        defaultFont         = font;
        defaultFontTexture  = font.texture;
    }
    
    public void SetFontDefault() => SetFont(backend.DefaultFont);
    
    public void SetFormatProvider(IFormatProvider provider) => formatProvider = provider;
    
    public Gui BeginGui(int width, int height)
    {
        var draw = BeginDraw(width, height);
        return draw.BeginGui();
    }
    
    public ImDraw BeginDraw(int width, int height)
    {
        // reset batcher state
        /* if (defaultFontTexture.IsDisposed) {    // TODO IM_TEX
            SetFontDefault();
        } */
        guiState.Reset();
        currentTexture      = defaultFontTexture;
        vertexStart         = 0;
        vertexCount         = 0;
        currentSamplerFilter= SamplerFilter.Linear;
        currentTransform    = Matrix4x4.Identity;
        currentBlendState   = BlendState.Alpha;
        currentScissor      = new RectVector2(Vector2.Zero, new Vector2(width, height));
        sortZIndex          = false;
        currentZIndex       = 0;
        currentSequence     = 0;

        drawCommands.Clear();
        scissorStack.Clear();
        transformStack.Clear();
        zIndexStack.Clear();
        samplerFilterStack.Clear();
        
        var draw = new ImDraw(this);
        draw.SetViewport(width, height);
        return draw;
    }
    
    public void Flush()
    {
        int pendingVertices = vertexCount - vertexStart;
        if (pendingVertices <= 0) {
            return;
        }

        int pendingQuads = pendingVertices / 4;

        var vertexView  = new MemoryView(vertexStart, pendingVertices);
        var indexView   = new MemoryView(0, pendingQuads * 6);
        vertexStart = vertexCount;

        // Batch.Draw(pass, config, bat.uniforms, texture, bat.currentSampler, vertexView, indexView);
        
        drawCommands.Add(new DrawCommand(
            zIndex:         currentZIndex,
            sequence:       currentSequence++, 
            texture:        currentTexture,
            vertexView:     vertexView,
            indexView:      indexView,
            blendState:     currentBlendState,
            projection:     projection,
            samplerFilter:  currentSamplerFilter,
            scissor:        currentScissor
        ));
    }
    
    protected void EndBatch()
    {
        Flush();
        if (vertexCount == 0) {
            return;
        }
        // Upload vertexBuffer with a single wgpu call
        gpuVertexBuffer.Write(0, vertexCount);

        var target   = drawList;
        var commands = drawCommands;
        if (target.Length < commands.Count) {
            target = drawList = new DrawCommand[commands.Count];
        }
        if (sortZIndex)
        {
            var segments = commandSegments;
            segments.Clear();
            SortCommands(commands, segments);
            int index = 0;
            foreach (var segment in segments) {
                for (int n = 0; n < segment.length; n++) {
                    target[index++] = commands[segment.index + n];
                }
            }
        } else {
            commands.CopyTo(target);
        }
    }
    
    private static void SortCommands(List<DrawCommand> commands, List<CmdSegment> segments)
    {
        // commands.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
        
        // Run-Length optimization - of commented Sort() above
        var command_0   = commands[0];
        int zIndex      = command_0.zIndex;
        var segment     = new CmdSegment { zIndex = zIndex, sequence = command_0.sequence, index = 0, length = 1 };
        
        for (int n = 1; n < commands.Count; n++)
        {
            var cmd = commands[n];
            if (zIndex == cmd.zIndex) {
                segment.length++;
                continue;
            }
            segments.Add(segment);
            zIndex              = cmd.zIndex;
            segment.zIndex      = zIndex;
            segment.sequence    = cmd.sequence;
            segment.index       = n;
            segment.length      = 1;
        }
        segments.Add(segment);
        
        segments.Sort((a, b) => (a.zIndex, a.sequence).CompareTo((b.zIndex, b.sequence)));
    }
#endregion
}

