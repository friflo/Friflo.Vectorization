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
        draw.Tui?.Space(window.Cursor, size);
        MoveCursor(size);

        bool isFired = IsFired(widgetState, isFocused);
        return new SpaceScope(this, new SpaceEnd(pos, size, isFired, isFocused, widgetState));
    }

    internal void EndSpace(in SpaceScope space)
    {
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
