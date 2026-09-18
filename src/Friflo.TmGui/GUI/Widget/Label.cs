// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;
using System.Numerics;
using Friflo.TmGui.TUI;

// ReSharper disable InconsistentNaming
// ReSharper disable ConvertIfStatementToConditionalTernaryExpression
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal void Label(ReadOnlySpan<char> name, TextColor textColor)
    {
        LabelReplay.Record(Recorder, name, textColor);
        
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

// --------------------------------------------- Step-Rendering ---------------------------------------------
internal readonly record struct Label(TextSpan name, Color32Span textColor);


internal sealed class LabelReplay : CmdReplay<Label>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        var cmd = commands[index];
        replay.widget.Label(replay.GetText(cmd.name), replay.GetColor(cmd.textColor));
    }
    
    internal static void Record(GuiRecorder? rec, ReadOnlySpan<char> name, TextColor textColor)
    {
        rec?.Record<LabelReplay, Label>(new Label(rec.GetTextSpan(name), rec.GetColorSpan(textColor)), false);
    }
}
