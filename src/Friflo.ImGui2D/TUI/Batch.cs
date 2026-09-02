// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.


using System;
using System.Collections.Generic;
using System.Numerics;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D.TUI;

public sealed class TuiBatch : ImBatch
{
    private             float                   lineScale;
    private             float                   lineHeight;
    private             int                     rectStart;
    private readonly    List<TuiRect>           rects       = [];
    private readonly    List<TuiRectCommand>    commands    = [];
    private readonly    List<char>              textBuffer  = [];
    private readonly    TuiBackend              backend;
    

    public TuiBatch(TuiBackend backend) : base(backend, 0) {
        this.backend = backend;
    }
    
    internal void Reset()
    {
        lineHeight  = backendDefaultFont.lineHeight;
        lineScale   = 1f / lineHeight;
        rectStart   = 0;   
        rects.Clear();
        commands.Clear();
        textBuffer.Clear();
    }
    
    internal void FlushRects()
    {
        var rectCount   = rects.Count;
        var view        = new RectView(rectStart, rectCount);
        rectStart       = rectCount;
        commands.Add(new TuiRectCommand(currentZIndex.value, currentSequence, view, currentScissor));
    }
    
    private void EndTuiBatch()
    {
        FlushRects();
    }
    
    public void DrawRectCommands()
    {
        EndTuiBatch();
        
        var scissor = new RectVector2(Vector2.Zero, viewport);
        foreach (var cmd in commands)
        {
            if (!cmd.scissor.Equals(scissor)) {
                scissor = cmd.scissor;
                // <set scissor call>    
            }
            // <draw command call>
        }
    }

    internal Vector2 DrawText(ReadOnlySpan<char> text, Vector2 position, Color32 color, Color32 background)
    {
        var textSpan    = new TextSpan { start = textBuffer.Count, len = text.Length };
        var tuiPos      = new TuiVector(position.X * lineScale, position.Y * lineScale);
        rects.Add(new TuiRect(textSpan, tuiPos, color, background));
        textBuffer.AddRange(text);
        return new Vector2(lineHeight * text.Length, lineHeight);
    }
    
    public void FillRect(Vector2 position, Vector2 size, Color32 background)
    {
        var tuiPos      = new TuiVector(position.X * lineScale, position.Y * lineScale);
        var tuiSize     = new TuiVector(size.X     * lineScale, size.Y     * lineScale);
        rects.Add(new TuiRect(tuiPos, tuiSize, background));
    }
    
    internal Vector2 Button(ReadOnlySpan<char> text, Vector2 position, Vector2 size, Color32 color, Color32 background)
    {
        var textSpan    = new TextSpan { start = textBuffer.Count, len = text.Length };
        var tuiPos      = new TuiVector(position.X * lineScale, position.Y * lineScale);
        rects.Add(new TuiRect(textSpan, tuiPos, color, background));
        textBuffer.AddRange(text);
        return new Vector2(lineHeight * text.Length, lineHeight);
    }
    
    internal Vector2 Checkbox(bool value, ReadOnlySpan<char> text, Vector2 position, Vector2 size, Color32 color, Color32 background)
    {
        var textSpan    = new TextSpan { start = textBuffer.Count, len = text.Length };
        var tuiPos      = new TuiVector(position.X * lineScale, position.Y * lineScale);
        rects.Add(new TuiRect(textSpan, tuiPos, color, background));
        textBuffer.AddRange(text);
        return new Vector2(lineHeight * text.Length, lineHeight);
    }

    public void Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width, Vector2 position, Vector2 size)
    {

    }

    public void DrawScrollbar(Vector2 position, Vector2 size, Color32 background)
    {
        var tuiPos      = new TuiVector(position.X * lineScale, position.Y * lineScale);
        var tuiSize     = new TuiVector(size.X     * lineScale, size.Y     * lineScale);
        rects.Add(new TuiRect(tuiPos, tuiSize, background));
    }
}