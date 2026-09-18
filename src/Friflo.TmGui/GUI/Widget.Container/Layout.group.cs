// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal VerticalScope BeginVertical(Dim size)
    {
        LayoutBeginReplay.Record(Recorder, new LayoutBegin(size, LayoutType.Vertical));
        
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Vertical, boundsSize);
        return new VerticalScope(this);
    }

    internal void EndVertical()
    {
        LayoutEndReplay.Record(Recorder, LayoutType.Vertical, false);
        
        PopLayout();
    }

    
    
    internal HorizontalScope BeginHorizontal(Dim size)
    {
        LayoutBeginReplay.Record(Recorder, new LayoutBegin(size, LayoutType.Horizontal));
        
        var boundsSize = Window.WidgetSize(size, default);
        PushLayout(LayoutDirection.Horizontal, boundsSize);
        return new HorizontalScope(this);
    }
    internal void EndHorizontal()
    {
        LayoutEndReplay.Record(Recorder, LayoutType.Horizontal, false);
        PopLayout();
    }
}


// --------------------------------------------- Step-Rendering ---------------------------------------------
internal enum LayoutType {
    Horizontal,
    Vertical,
}

internal readonly record struct LayoutBegin (Dim size, LayoutType type);


internal sealed class LayoutBeginReplay : CmdReplay<LayoutBegin>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        var cmd = commands[index];
        if (cmd.type == LayoutType.Horizontal) {
            replay.widget.BeginHorizontal(cmd.size);
        } else {
            replay.widget.BeginVertical(cmd.size);
        }
        LayoutEndReplay.Record(replay.recorder, cmd.type, true);
    }
    
    internal static void Record(GuiRecorder? rec, LayoutBegin cmd)
    {
        rec?.Record<LayoutBeginReplay, LayoutBegin>(cmd, false);
    }
}

internal sealed class LayoutEndReplay : CmdReplay<LayoutType>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.PopStackEnd();
        if (commands[index] == LayoutType.Horizontal) {
            replay.widget.EndHorizontal();
        } else {
            replay.widget.EndVertical();
        }
    }
    
    internal static void Record(GuiRecorder? rec, LayoutType type, bool isPush)
    {
        rec?.Record<LayoutEndReplay, LayoutType>(type, isPush);
    }
}
