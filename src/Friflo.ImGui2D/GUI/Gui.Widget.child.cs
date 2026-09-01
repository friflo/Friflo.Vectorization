// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using System.Numerics;

// ReSharper disable once CheckNamespace
namespace Friflo.ImGui2D;


public readonly ref partial struct GuiWidget
{
#region child
	internal ChildScope BeginChild(WidgetID childId, Dim size)
	{
	    var window = Window;
	    var parentStartCursor = window.Cursor;
	    window.PushScope(childId);

	    var outerSize = window.WidgetSize(size, default);
	    if (size.IsBounded) {
	        draw.PushScissor(parentStartCursor, outerSize);
	    }
	    window.SetCursor(parentStartCursor + Sizes.ChildPadding.Min);
	    var innerLayoutSize	= outerSize - Sizes.ChildPadding.Size;
	    PushLayout(LayoutDirection.Vertical, innerLayoutSize);

	    return new ChildScope(this, parentStartCursor, size, outerSize);
	}

	internal void EndChild(in ChildScope scope)
	{
		var window = Window;
	    var padding = Sizes.ChildPadding;
	    var contentSize = PopLayout();

	    if (scope.requestedSize.IsBounded) {
	        draw.PopScissor();
	    }
	    window.PopScope();

	    var finalChildSize = new Vector2(
	        scope.requestedSize.IsAutoWidth  ? contentSize.X + padding.Size.X : scope.outerSize.X,
	        scope.requestedSize.IsAutoHeight ? contentSize.Y + padding.Size.Y : scope.outerSize.Y
	    );
	    window.SetCursor(scope.startCursor);
	    MoveCursor(finalChildSize);
	}
#endregion


#region scroll area
	internal ScrollAreaScope BeginScrollArea(int childId, Dim size)
	{
	    var window		= Window;
	    var startCursor = window.Cursor;
	    window.PushScope(childId);

	    // Compute outer bounds for the scroll area viewport
	    var outerSize = window.WidgetSize(size, default);

	    // Scroll areas ALWAYS require scissor clipping against their calculated outer size
	    draw.PushScissor(startCursor, outerSize);
	    draw.FillRect(startCursor, outerSize, Colors.ScrollAreaColor);

	    var scrollRect = PushScrollArea(childId, startCursor, outerSize, Sizes.ChildPadding);

	    window.SetCursor(scrollRect.pos);
	    PushLayout(LayoutDirection.Vertical, scrollRect.size);

	    return new ScrollAreaScope(this, childId, startCursor, size, outerSize);
	}

	internal void EndScrollArea(in ScrollAreaScope scope)
	{
	    var window	= Window;
	    var padding = Sizes.ChildPadding;
	    
		draw.PopScissor();

	    // Measure base content including padding and focus outline clearance
	    var rawContent = PopLayout();
	    var scrollSize = rawContent + padding.Size;
	    
	    PopScrollArea(scope.childId, scope.startCursor, scrollSize, scope.outerSize);

	    window.PopScope();
	    
	    window.SetCursor(scope.startCursor);
	    MoveCursor(scope.outerSize + padding.Max);
	}
#endregion
}

