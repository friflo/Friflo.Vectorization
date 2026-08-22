// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;


// ReSharper disable once CheckNamespace
namespace Friflo.WGPU.ImDraw;


public readonly ref struct Gui : IDisposable
{
    public readonly GuiWidget   widget;     // 40 bytes
    public          Draw2D      Draw        => widget.draw;
    public          float       LineHeight  => widget.draw.DefaultFont.lineHeight;
    
    internal Gui(Draw2D draw, Batch2D batch) {
        widget = new GuiWidget(draw, batch);
    }
    
    public void Dispose() {
        widget.draw.Dispose();
    }
    
    public void SetNextWindowPos(Vector2 position)  => widget.SetNextWindowPos(position);
    public void SetNextWindowSize(Vector2 size)     => widget.SetNextWindowSize(size);
    
    public WindowScope  BeginWindow(string title)   => widget.BeginWindow(title);
    public void         EndWindow()                 => widget.EndWindow();
    
    public void Label(ReadOnlySpan<char> name, Color32 textColor = default)
        => widget.Label(name, textColor);
    
    public bool Button(ReadOnlySpan<char> name, GuiStyle? style = null, WidgetID id = default)
        => widget.Button(name, style, id);
    
    public bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style = null, WidgetID id = default)
        => widget.Checkbox(name, ref value, style, id);
    
    public bool Slider(float width, ReadOnlySpan<char> name, ref float value, ReadOnlySpan<char> format, float min, float max, GuiStyle? style = null, WidgetID id = default)
        => widget.Slider(width, name, ref value, format, min, max, style, id);
    
    public bool ReserveSpace(out Vector2 pos, Vector2 size, out bool isFocused, out WidgetState widgetState, WidgetID id = default)
        => widget.ReserveSpace(out pos, size, out isFocused, out widgetState, id);
    
    public void DrawFocusRect(Vector2 pos, Vector2 size, bool isFocused, float margin = 4f)
        => widget.DrawFocusRect(pos, size, isFocused, margin);
    
    public StyleScope       PushStyle(GuiStyle style)   => widget.PushStyle(style);
    public void             PopStyle()                  => widget.PopStyle();
    
    public VerticalScope    BeginVertical()             => widget.BeginVertical();
    public void             EndVertical()               => widget.EndVertical();
    
    public HorizontalScope  BeginHorizontal()           => widget.BeginHorizontal();
    public void             EndHorizontal()             => widget.EndHorizontal();
}
