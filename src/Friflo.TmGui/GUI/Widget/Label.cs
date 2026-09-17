// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;

// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal void Label(ReadOnlySpan<char> name, TextColor textColor)
    {
        var window = Window;
        textColor = textColor.IsNone ? Colors.TextColor : textColor;
        
        var tui = draw.Tui;
        Vector2 size;
        if (tui != null) {
            size = tui.DrawLabel(name, window.Cursor, textColor);
        } else {
            size = draw.DrawText(name, window.Cursor, textColor);
        }
        MoveCursor(size);
    }
}