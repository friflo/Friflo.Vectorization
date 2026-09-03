// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public readonly ref partial struct GuiWidget
{
    internal bool Slider(ReadOnlySpan<char> name, ref float value, float min, float max, float width, ReadOnlySpan<char> format, GuiStyle? style, WidgetID id)
    {
        var window      = Window;
        using var _     = UseStyle(style);
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);

        var padding     = Sizes.FramePadding;
        float height    = LineHeight + padding.Vertical;
        var pos         = window.Cursor;
        var totalSize   = new Vector2(width, height);
        var isHover     = window.IsHoverAtCapture(pos, totalSize, draw);
        bool isFocused  = RegisterFocusable(widgetId, pos, totalSize);
        var widgetState = GetWidgetState(isHover, widgetId);
        bool changed    = false;
        
        if (widgetState == WidgetState.Down) {
            float t = Math.Clamp((input.MousePos.X - pos.X) / width, 0f, 1f);
            float newValue = min + t * (max - min);
            
            if (newValue != value) {
                value = newValue;
                changed = true;
            }
        }
        var labelText = StringBuilder().AppendFloat(value, format.IsEmpty ? "F1" : format, FormatProvider);
        var tui = draw.Tui;
        if (tui != null) {
            tui.Slider(labelText.Span(), ref value, min, max, width, pos, totalSize);
        } else {
            draw.FillRectRounded  (pos, totalSize, Sizes.CornerRadius, Colors.SliderState(widgetState), GuiSizes.CornerSegments); // background
            // Fill bar
            float tVal = Math.Clamp((value - min) / (max - min), 0f, 1f);
            var fillSize = new Vector2(width * tVal, height);
            
            draw.FillRectRounded(pos, fillSize, Sizes.CornerRadius, Colors.SliderColor, GuiSizes.CornerSegments);
            draw.StrokeRectRounded(pos, totalSize, Sizes.CornerRadius, 2, Colors.ButtonBorder, GuiSizes.CornerSegments);
            draw.DrawTextInRect(labelText.Span(), pos, totalSize, TextAlignment.Center, VerticalAlignment.Middle, Colors.TextColor);
        }
        if (isFocused) {
            DrawFocus(pos, totalSize);
            window.EnsureVisibleInScrollArea(pos, totalSize);
        }
        MoveCursor(totalSize);
        return changed;
    }
}
