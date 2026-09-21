// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;

// ReSharper disable InconsistentNaming
// ReSharper disable SuggestVarOrType_BuiltInTypes
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
    internal void Spacer(float size)
    {
        SpacerReplay.Record(Recorder, size);
        
        var window      = Window;
        var spaceSize   = window.CurrentLayout.direction == LayoutDirection.Horizontal ? new Vector2(size, 0) : new Vector2(0, size);
        MoveCursor(spaceSize);
    }
    
   
    internal SpaceScope BeginSpace(Vector2 size, WidgetID id)
    {
        SpaceBeginReplay.Record(Recorder, new SpaceBegin(size, id));
            
        var window      = Window;
        var pos         = window.Cursor;
        var widgetState = WidgetState.None;
        var isFocused   = false;

        if (id.IsValid) {
            int parentHash  = window.GetCurrentScopeHash();
            int widgetId    = id.Resolve(parentHash);
            
            bool isHover    = window.IsHoverAtCapture(pos, size, draw);
            widgetState     = GetWidgetState(isHover, widgetId);
            isFocused       = RegisterFocusable(widgetId, pos, size);
        }
        // draw.Tui?.Space(window.Cursor, size);
        MoveCursor(size);

        bool isFired = IsFired(widgetState, isFocused);
        return new SpaceScope(this, new SpaceEnd(pos, size, isFired, isFocused, widgetState));
    }

    internal void EndSpace(in SpaceScope space)
    {
        SpaceEndReplay.Record(Recorder, space.end, false);
            
        if (!space.end.isFocused) return;
        DrawFocus(space.end.pos, space.end.size);
        EnsureVisibleInScrollArea(space.end.pos, space.end.size);
    }
}

// --------------------------------------------- Step-Rendering --------------------------------------------- 
public readonly record struct Spacer(float size);

public sealed class SpacerReplay : CmdReplay<Spacer>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.widget.Spacer(commands[index].size);
    }
    
    public static void Record(GuiRecorder? rec, float size)
    {
        rec?.Record<SpacerReplay, Spacer>(new Spacer(size), false);
    }
}


internal readonly record struct SpaceBegin(Vector2 size, WidgetID id);

internal sealed class SpaceBeginReplay : CmdReplay<SpaceBegin>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        var cmd = commands[index];
        var scope = replay.widget.BeginSpace(cmd.size, cmd.id);
        SpaceEndReplay.Record(replay.recorder, scope.end, true);
    }
    
    internal static void Record(GuiRecorder? rec, SpaceBegin cmd)
    {
        rec?.Record<SpaceBeginReplay, SpaceBegin>(cmd, false);
    }
}

internal sealed class SpaceEndReplay : CmdReplay<SpaceEnd>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.PopStackEnd();
        replay.widget.EndSpace(new SpaceScope(replay.widget, commands[index]));
    }
    
    internal static void Record(GuiRecorder? rec, SpaceEnd end, bool isPush)
    {
        rec?.Record<SpaceEndReplay, SpaceEnd>(end, isPush);
    }
}