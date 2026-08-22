// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Numerics;


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct Gui : IDisposable
{
    public readonly     GuiWidget   widget;     // 40 bytes
    
    public ref readonly GuiColor    Color       { [DebuggerStepThrough] get => ref widget.Color; }
    public              Draw2D      Draw        => widget.draw;
    public              float       LineHeight  => widget.draw.DefaultFont.lineHeight;
    
    internal Gui(Draw2D draw, Batch2D batch) {
        widget = new GuiWidget(draw, batch);
    }
    
    public void Dispose() {
        widget.draw.Dispose();
    }
    
    public WindowScope  BeginWindow(string title, Vector2? pos = null, Vector2? size = null)    => widget.BeginWindow(title, pos, size);
    public void         EndWindow()                                                             => widget.EndWindow();
    
    /// <summary>Begins a clipped, isolated child area within the current window.</summary>
    /// <param name="size">Target size. Use &gt; 0 for fixed dimensions or 0 for dynamic auto-fit/remaining space.</param>
    public ChildScope   BeginChild(WidgetID id, Vector2 size)                   => widget.BeginChild(id, size);
    public void         EndChild(Vector2 parentStartCursor, Vector2 childSize)  => widget.EndChild(parentStartCursor, childSize);
    
    public void Label(ReadOnlySpan<char> name, Color32 textColor = default)
        => widget.Label(name, textColor);
    
    public bool Button(ReadOnlySpan<char> name, GuiStyle? style = null, WidgetID id = default)
        => widget.Button(name, style, id);
    
    public bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style = null, WidgetID id = default)
        => widget.Checkbox(name, ref value, style, id);
    
    public bool Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width = 0, ReadOnlySpan<char> format = default, GuiStyle? style = null, WidgetID id = default)
        => widget.Slider(name, ref value, min, max, width, format, style, id);
    
    /// <summary> Reserves space for custom drawing. Must provide an <paramref name="id"/> if interactive. </summary>
    public SpaceScope       BeginSpace(Vector2 size, WidgetID id = default) => widget.BeginSpace(size, id);
    public void             EndSpace(SpaceScope space)                      => widget.EndSpace(space);
    
    public StyleScope       PushStyle(GuiStyle style)   => widget.PushStyle(style);
    public void             PopStyle()                  => widget.PopStyle();

    public void             Spacer(float size = 20f)    => widget.Spacer(size);
    public VerticalScope    BeginVertical()             => widget.BeginVertical();
    public void             EndVertical()               => widget.EndVertical();
    
    public HorizontalScope  BeginHorizontal()           => widget.BeginHorizontal();
    public void             EndHorizontal()             => widget.EndHorizontal();
    
    public ScrollAreaScope  BeginScrollArea(int childId, Vector2 size)
        => widget.BeginScrollArea(childId, size);
    public void             EndScrollArea(int childId, Vector2 parentStartCursor, Vector2 childSize)
        => widget.EndScrollArea(childId, parentStartCursor, childSize);
    
}
