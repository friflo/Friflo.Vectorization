// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;

// ReSharper disable InconsistentNaming
// ReSharper disable once CheckNamespace
namespace Friflo.TmGui;


public readonly ref partial struct GuiWidget
{
	internal ScrollAreaScope BeginScrollArea(int childId, Dim size)
	{
        ScrollAreaBeginReplay.Record(Recorder, new ScrollAreaBegin(childId, size));
            
	    var window		= Window;
	    var startCursor = window.Cursor;
	    window.PushScope(childId);

	    // Compute outer bounds for the scroll area viewport
	    var minHeight = 2 * LineHeight;
	    var outerSize = window.WidgetSize(size, new Vector2(minHeight, minHeight));

	    // Scroll areas ALWAYS require scissor clipping against their calculated outer size
	    draw.PushScissor(startCursor, outerSize);
	    var tui = draw.Tui;
	    if (tui != null) {
		    tui.FillRect(startCursor, outerSize, Colors.ScrollAreaColor);
	    } else {
		    draw.FillRect(startCursor, outerSize, Colors.ScrollAreaColor);
	    }
	    var scrollRect = PushScrollArea(childId, startCursor, outerSize, Sizes.ChildPadding);

	    window.SetCursor(scrollRect.pos);
	    PushLayout(LayoutDirection.Vertical, scrollRect.size);

	    return new ScrollAreaScope(this, new ScrollAreaEnd(childId, startCursor, outerSize));
	}

	internal void EndScrollArea(in ScrollAreaScope scope)
	{
        ScrollAreaEndReplay.Record(Recorder, scope.end, false);
            
	    var window	= Window;
	    var padding = Sizes.ChildPadding;
	    
		draw.PopScissor();

	    // Measure base content including padding and focus outline clearance
	    var rawContent = PopLayout();
	    var scrollSize = rawContent + padding.Size;
	    
	    PopScrollArea(scope.end.childId, scope.end.startCursor, scope.end.outerSize, scrollSize, Colors.ScrollAreaColor, true);

	    window.PopScope();
	    
	    window.SetCursor(scope.end.startCursor);
	    MoveCursor(scope.end.outerSize);
	}
}


// --------------------------------------------- Step-Rendering ---------------------------------------------
internal readonly record struct ScrollAreaBegin(int childId, Dim size);


internal sealed class ScrollAreaBeginReplay : CmdReplay<ScrollAreaBegin>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        var cmd = commands[index];
        var scope = replay.widget.BeginScrollArea(cmd.childId, cmd.size);
        ScrollAreaEndReplay.Record(replay.recorder, scope.end, true);
    }
    
    internal static void Record(GuiRecorder? rec, ScrollAreaBegin cmd)
    {
        rec?.Record<ScrollAreaBeginReplay, ScrollAreaBegin>(cmd, false);
    }
}

internal sealed class ScrollAreaEndReplay : CmdReplay<ScrollAreaEnd>
{
    protected internal override void Replay(in Replay replay, int index)
    {
        replay.PopStackEnd();
        replay.widget.EndScrollArea(new ScrollAreaScope(replay.widget, commands[index]));
    }
    
    internal static void Record(GuiRecorder? rec, ScrollAreaEnd end, bool isPush)
    {
        rec?.Record<ScrollAreaEndReplay, ScrollAreaEnd>(end, isPush);
    }
}

