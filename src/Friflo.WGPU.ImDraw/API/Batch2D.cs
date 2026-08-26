// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Friflo.GPU;
using Friflo.WGPU;


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


public abstract class Batch2D : IDisposable
{
#region internal
    private  readonly   DrawModule          drawModule;
    internal readonly   Memory<Vertex2D>    vertexBuffer;
    internal readonly   ImBuffer<Vertex2D>  gpuVertexBuffer;
    internal readonly   ImBuffer<uint>      gpuIndexBuffer;
    
    internal readonly   List<DrawCommand>   drawCommands 	    = [];
    internal readonly   List<CmdSegment>    commandSegments     = [];
    internal readonly   Stack<RectVector2>  scissorStack        = [];
    internal readonly   Stack<Matrix4x4>    transformStack      = [];
    internal readonly   Stack<int>          zIndexStack         = [];
    internal readonly   Stack<SamplerFilter>samplerFilterStack  = [];
    private  readonly   StringBuilder       stringBuilder       = new(512,512); // => first chunk: 512 chars
    internal readonly   GuiState            guiState            = new();

    // --- resources owned by DrawModule
    internal readonly   GuiHost             host;
    public   readonly   GuiInput            input;
    internal readonly   GpuSampler          samplerLinear;              // the default sampler
    internal readonly   GpuSampler          samplerNearest;
    internal            Font                defaultFont;
    internal            ImTexture           defaultFontTexture;
    
    // --- Draw2D - state
    internal            IFormatProvider     formatProvider;
    internal            Vector2             viewport;
    internal            Matrix4x4           defaultOrtho;
    internal            Matrix4x4           currentTransform;
    internal            BlendState          currentBlendState;
    internal            SamplerFilter       currentSamplerFilter;
    internal            RectVector2         currentScissor;
    internal            bool                sortZIndex;
    internal            int                 currentZIndex;
    internal            int                 currentSequence;
    internal            Matrix4x4           projection;
    internal            int                 vertexStart;                // start of next Draw()
    internal            int                 vertexCount;
    internal            ImTexture           currentTexture;

    
    protected Batch2D(ImGuiBackend backend, GpuDevice device, int maxVertices)
    {
        if (!device.TryGetModule(out drawModule)) {
            drawModule = new DrawModule(device);
            device.AddModule(drawModule);
        }
        formatProvider  = CultureInfo.InvariantCulture;
        host            = drawModule.guiModule.host;
        input           = drawModule.guiModule.input;
        
        // --- vertex & index buffer - to draw quads
        int maxQuads   = maxVertices / 4;
        int maxIndices = maxQuads * 6;

        gpuVertexBuffer = backend.CreateVertexBuffer(maxVertices);
        vertexBuffer = gpuVertexBuffer.Memory;


        gpuIndexBuffer = backend.CreateIndexBuffer(maxIndices);
        
        // generate quad indexes only once
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
        
        defaultFont             = drawModule.defaultFont;
        defaultFontTexture      = drawModule.defaultFont.texture;
        samplerLinear           = drawModule.samplerLinear;
        samplerNearest          = drawModule.samplerNearest;
        currentSamplerFilter    = SamplerFilter.Linear;
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
    
    public void AddEvent(in ImEvent ev) => input.AddEvent(ev);
    
    public void SetFont(Font font) {
        defaultFont         = font;
        defaultFontTexture  = font.texture;
    }
    
    public void SetFontDefault() => SetFont(drawModule.defaultFont);
    
    public void SetFormatProvider(IFormatProvider provider) => formatProvider = provider;
    
    public Gui BeginGui(int width, int height)
    {
        var draw = BeginDraw2D(width, height);
        return draw.BeginGui();
    }
    
    public Draw2D BeginDraw2D(int width, int height)
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
        
        var draw = new Draw2D(this);
        draw.SetViewport(width, height);
        return draw;
    }
#endregion
}

