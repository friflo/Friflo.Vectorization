// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Numerics;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui;

public static class HorizontalAlignment  // move const values to UI class
{
    /// <summary> <i>Note:</i> Avoid using <see cref="Left"/> use usual <see cref="Gui.BeginHorizontal"/> instead. </summary>
    public const float Left     = 0f;
    public const float Center   = 0.5f;
    public const float Right    = 1f;
}

public static class UI
{
    /** Fill width and height          */   public static readonly Vector2  Fill    = new Vector2(float.NaN, float.NaN);
    /** Fill width, height via Content */   public static readonly Vector2  FillX   = new Vector2(float.NaN,        0f);
    /** Fill height, width via Content */   public static readonly Vector2  FillY   = new Vector2(       0f, float.NaN);
}

public readonly ref struct Gui
{
    public readonly     GuiWidget   widget;     // 32 bytes
    
    public ref readonly GuiColors   Colors      { [DebuggerStepThrough] get => ref widget.Colors; }
    public ref readonly GuiSizes    Sizes       { [DebuggerStepThrough] get => ref widget.Sizes; }
    public              ImDraw      Draw        => widget.draw;
    public              float       LineHeight  => widget.draw.Font.lineHeight;
    public              GuiInput    Input       => widget.input;
    
    internal Gui(ImDraw draw, ImBatch batch) {
        widget = new GuiWidget(draw, batch);
    }
    
    public WindowScope  BeginWindow(string title, Vector2? pos = null, Vector2? size = null)    => widget.BeginWindow(title, pos, size);
    public void         EndWindow()                                                             => widget.EndWindow();
    
    /// <summary>Begins a clipped, isolated child area within the current window.</summary>
    /// <param name="size">Target size. Use &gt; 0 for fixed dimensions or 0 for dynamic auto-fit/remaining space.</param>
    public ChildScope   BeginChild(WidgetID id, Vector2 size)                   => widget.BeginChild(id, size);
    public void         EndChild(in ChildScope scope)  => widget.EndChild(scope);
    
    public void Label(ReadOnlySpan<char> name, Color32 textColor = default)
        => widget.Label(name, textColor);
    
    public bool Button(ReadOnlySpan<char> name, Vector2 size = default, GuiStyle? style = null, WidgetID id = default)
        => widget.Button(name, size, style, id);
    
    public bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style = null, WidgetID id = default)
        => widget.Checkbox(name, ref value, style, id);
    
    public bool Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width = 0, ReadOnlySpan<char> format = default, GuiStyle? style = null, WidgetID id = default)
        => widget.Slider(name, ref value, min, max, width, format, style, id);
    
    /// <summary> Reserves space for custom drawing. Must provide an <paramref name="id"/> if interactive. </summary>
    public SpaceScope       BeginSpace(Vector2 size, WidgetID id = default) => widget.BeginSpace(size, id);
    public void             EndSpace(SpaceScope space)                      => widget.EndSpace(space);
    
    public StyleScope       PushStyle(GuiStyle style)   => widget.PushStyle(style);
    public void             PopStyle()                  { if (widget.IsSet) widget.PopStyle(); }

    public void             Spacer(float size = 20f)                => widget.Spacer(size);
    public VerticalScope    BeginVertical(Vector2 size = default)   => widget.BeginVertical(size);
    public void             EndVertical()                           => widget.EndVertical();
    
    public HorizontalScope  BeginHorizontal(Vector2 size = default) => widget.BeginHorizontal(size);
    public void             EndHorizontal()                         => widget.EndHorizontal();
    
    /// <summary>Begins a horizontal layout group with flexible alignment.</summary>
    /// <param name="align">Alignment position. <br/>Use predefined values like <see cref="HorizontalAlignment.Center"/> (0.5f) or <see cref="HorizontalAlignment.Right"/> (1.0f), or custom floats between 0.0f and 1.0f.</param>
    public HorizontalCenterScope    BeginHorizontalAligned(int id, float align, Vector2 size = default) => widget.BeginHorizontalAligned(id, align, size);
    public void                     EndHorizontalAligned(in HorizontalCenterScope scope) => widget.EndHorizontalAligned(scope);
    
    public ScrollAreaScope  BeginScrollArea(int childId, Vector2 size)  => widget.BeginScrollArea(childId, size);
    public void             EndScrollArea(in ScrollAreaScope scope)     => widget.EndScrollArea(scope);
    
}
