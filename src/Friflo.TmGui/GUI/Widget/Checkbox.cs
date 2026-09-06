// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal bool Checkbox(ReadOnlySpan<char> name, ref bool value, GuiStyle? style, WidgetID id)
    {
        var window  = Window;
        using var _ = UseStyle(style);
        int parentHash  = window.GetCurrentScopeHash();
        int widgetId    = id.Resolve(name, parentHash);

        var padding = Sizes.FramePadding;
        var tui     = draw.Tui;
        
        float boxSize   = LineHeight + padding.Vertical;
        var pos         = window.Cursor;
        var textSize    = draw.MeasureText(name);

        var boxRectSize = tui != null ? new Vector2(3 * tui.CharWidth, LineHeight) : new Vector2(boxSize, boxSize);  // '[x] ' / quadratic box
        var totalSize   = boxRectSize + new Vector2(padding.Size.X + textSize.X, 0);
        var isHover     = window.IsHoverAtCapture(pos, totalSize, draw);
        bool isFocused  = RegisterFocusable(widgetId, pos, totalSize);
        var widgetState = GetWidgetState(isHover, widgetId);
        bool isToggled  = IsFired(widgetState, isFocused);
        if (isToggled) {
            value = !value;
        }
        var boxColor = Colors.ButtonState(widgetState);
        if (tui != null) {
            tui.Checkbox(value, name, pos, totalSize, Colors.TextColor, boxColor);
        } else {
            draw.FillRectRounded  (pos, boxRectSize, Sizes.CornerRadius, boxColor, GuiSizes.CornerSegments); // background
            draw.StrokeRectRounded(pos, boxRectSize, Sizes.CornerRadius, 2, Colors.ButtonBorder, GuiSizes.CornerSegments);
            if (value) {
                var fillOffset = new Vector2(8, 8);
                draw.FillRectRounded(pos + fillOffset, boxRectSize - 2 * fillOffset, Sizes.CornerRadius, Colors.TextColor, GuiSizes.CornerSegments);
            }
            var textPos = new Vector2(pos.X + boxSize + padding.Min.X, pos.Y + padding.Min.Y);
            draw.DrawText(name, textPos, Colors.TextColor);
        }
        if (isFocused) {
            DrawFocus(pos, boxRectSize);
            window.EnsureVisibleInScrollArea(pos, boxRectSize);
        }
        MoveCursor(totalSize);
        return isToggled;
    }
}
