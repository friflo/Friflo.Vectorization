// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal bool Button(ReadOnlySpan<char> name, Dim size, GuiStyle? style, WidgetID id)
    {
        var window = Window;
        using var _ = UseStyle(style);

        int parentHash = window.GetCurrentScopeHash();
        int widgetId   = id.Resolve(name, parentHash);

        var tui         = draw.Tui;
        var pos         = window.Cursor;
        var textSize    = draw.MeasureText(name) ;

        // Calculate final pixel footprint based on measured text size as content fallback
        var defaultSize = textSize + Sizes.FramePadding.Size;
        var finalSize   = window.WidgetSize(size, defaultSize);
        if (tui != null) finalSize.X += 2 * tui.CharWidth;  // TUI:  '[]'  e.g. [Button]   

        var isHover     = window.IsHoverAtCapture(pos, finalSize, draw);
        bool isFocused  = RegisterFocusable(widgetId, pos, finalSize);
        var widgetState = GetWidgetState(isHover, widgetId);

        if (tui != null) {
            tui.Button(name, pos, finalSize, Colors.ButtonText, Colors.ButtonState(widgetState));
        } else {
            draw.FillRectRounded  (pos, finalSize, Sizes.CornerRadius, Colors.ButtonState(widgetState), GuiSizes.CornerSegments);
            draw.StrokeRectRounded(pos, finalSize, Sizes.CornerRadius, 2, Colors.ButtonBorder, GuiSizes.CornerSegments);
            draw.DrawTextInRect(name, pos + Sizes.FramePadding.Min, textSize, TextAlignment.Center, VerticalAlignment.Middle, Colors.ButtonText);
        }
        if (isFocused) {
            DrawFocus(pos, finalSize);
            window.EnsureVisibleInScrollArea(pos, finalSize);
        }
        
        MoveCursor(finalSize);
        
        return IsFired(widgetState, isFocused);
    }
}