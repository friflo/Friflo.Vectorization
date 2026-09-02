// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public readonly ref partial struct GuiWidget
{
    internal void Label(ReadOnlySpan<char> name, Color32 textColor)
    {
        var window = Window;
        if (textColor.Packed == 0) textColor = Colors.TextColor;
        
        var tui = draw.batch.tui;
        Vector2 size;
        if (tui != null) {
            size = tui.DrawText(name, window.Cursor, textColor, default);
        } else {
            size = draw.DrawText(name, window.Cursor, textColor);
        }
        MoveCursor(size);
    }
}