// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal StyleScope PushStyle(GuiStyle style)
    {
        StyleBeginReplay.Record(Recorder, new StyleBegin(style));
            
        var revertStyles = guiState.revertStyles;
        var length       = revertStyles.Length;
        if (guiState.revertStylesCount >= length) {
            revertStyles = new RevertStyle[Math.Max(4, 2 * length)]; 
            Array.Copy(guiState.revertStyles,  revertStyles, length);
            guiState.revertStyles = revertStyles; 
        }
        ref var revertStyle = ref revertStyles[guiState.revertStylesCount++];
        guiState.currentStyle.PushOverrides(style, ref revertStyle);
        return new StyleScope(this);
    }
    
    internal void PopStyle()
    {
        StyleEndReplay.Record(Recorder, new StyleEnd(), false);
        
        ref var revertStyle = ref guiState.revertStyles[--guiState.revertStylesCount];
        guiState.currentStyle.PopOverrides(revertStyle);
    }
}


// --------------------------------------------- Step-Rendering --------------------------------------------- 
internal readonly record struct StyleBegin(GuiStyle style);
internal readonly        struct StyleEnd;

internal sealed class StyleBeginReplay : CmdReplay<StyleBegin>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.widget.PushStyle(commands[index].style);
        StyleEndReplay.Record(replay.recorder, new StyleEnd(), true);
    }
    
    internal static void Record(GuiRecorder? rec, StyleBegin cmd)
    {
        rec?.Record<StyleBeginReplay, StyleBegin>(cmd, false);
    }
}

internal sealed class StyleEndReplay : CmdReplay<StyleEnd>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.PopStackEnd();
        replay.widget.PopStyle();
    }
    
    internal static void Record(GuiRecorder? rec, StyleEnd end, bool isPush)
    {
        rec?.Record<StyleEndReplay, StyleEnd>(end, isPush);
    }
}
